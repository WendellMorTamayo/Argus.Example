using System.Linq.Expressions;
using Argus.Example.Data;
using Argus.Example.Data.Cbor.Datums;
using Argus.Example.Data.Cbor.Redeemers;
using Argus.Example.Data.Entity;
using Argus.Example.Data.Enum;
using Argus.Example.Data.Extensions;
using Argus.Sync.Extensions;
using Argus.Sync.Reducers;
using Argus.Sync.Utils;
using Chrysalis.Cardano.Core.Extensions;
using Chrysalis.Cardano.Core.Types.Block;
using Chrysalis.Cardano.Core.Types.Block.Transaction.Body;
using Chrysalis.Cardano.Core.Types.Block.Transaction.Input;
using Chrysalis.Cardano.Core.Types.Block.Transaction.Output;
using Chrysalis.Cardano.Sundae.Types.Common;
using Chrysalis.Cbor.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;

namespace Argus.Example.Reducers;

public class OrderBySlotReducer(
    IDbContextFactory<ArgusDbContext> dbContextFactory,
    IConfiguration configuration
) : IReducer<OrderBySlot>
{
    private readonly string _orderBookScriptHash = configuration.GetValue("OrderBook", "0f45963b8e895bd46839bbcf34185993440f26e3f07c668bd2026f92");

    public async Task RollBackwardAsync(ulong slot)
    {
        await using ArgusDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        List<OrderBySlot> spentOutputs = await dbContext.OrdersBySlot
            .Where(o => o.SpentSlot >= slot)
            .ToListAsync();

        if (!spentOutputs.Any()) return;

        List<OrderBySlot> updatedEntries = [.. spentOutputs.Select(existing => existing with
        {
            Status = OrderStatus.Active,
            SpentSlot = null,
            SpentTxHash = null,
            BuyerAddress = null
        })];

        foreach (OrderBySlot existing in spentOutputs)
        {
            EntityEntry<OrderBySlot>? trackedEntity = dbContext.ChangeTracker.Entries<OrderBySlot>()
                .FirstOrDefault(e =>
                    e.Entity.Slot == existing.Slot &&
                    e.Entity.TxHash == existing.TxHash &&
                    e.Entity.Index == existing.Index);

            if (trackedEntity is not null)
            {
                trackedEntity.State = EntityState.Detached;
            }
        }

        dbContext.OrdersBySlot.UpdateRange(updatedEntries);
        dbContext.OrdersBySlot.RemoveRange(
            dbContext.OrdersBySlot
            .AsNoTracking()
            .Where(o => o.Slot >= slot && o.SpentSlot == null)
        );

        await dbContext.SaveChangesAsync();
    }

    public async Task RollForwardAsync(Block block)
    {
        await using ArgusDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        IEnumerable<TransactionBody> transactions = block.TransactionBodies();

        transactions.ToList().ForEach(transaction => ProcessOutputs(
            transaction,
            dbContext,
            block
        ));

        List<(string txHash, ulong index)> inputOutRefs = [.. transactions
            .SelectMany(tx => tx.Inputs(), (tx, input) => (txHash: input.TransactionId(), index: input.Index.Value))];

        Expression<Func<OrderBySlot, bool>> predicate = PredicateBuilder.False<OrderBySlot>();
        inputOutRefs.ForEach(input =>
            predicate = predicate.Or(o => o.TxHash == input.txHash && o.Index == input.index));

        List<OrderBySlot> dbEntries = await dbContext.OrdersBySlot
            .Where(predicate)
            .ToListAsync();

        List<OrderBySlot> localEntries = [.. dbContext.OrdersBySlot.Local.Where(e => inputOutRefs.Any(input => input.txHash == e.TxHash && input.index == e.Index))];

        List<OrderBySlot> allEntries = [.. dbEntries
            .Concat(localEntries)
            .GroupBy(e => (e.TxHash, e.Index))
            .Select(g => g.First())];

        ProcessInputs(block, allEntries, dbContext);

        await dbContext.SaveChangesAsync();
    }

    private void ProcessOutputs(TransactionBody tx, ArgusDbContext dbContext, Block block)
    {
        ulong slot = block.Slot() ?? 0;
        string txHash = tx.Id();

        tx.Outputs().Select((output, index) => new { Output = output, Index = (ulong)index })
            .ToList().ForEach(e =>
            {
                string? outputBech32Addr = e.Output.Address()?.GetBaseAddressBech32();

                if (string.IsNullOrEmpty(outputBech32Addr) || !outputBech32Addr.StartsWith("addr")) return;

                string pkh = Convert.ToHexString(e.Output.Address()!.GetPublicKeyHash()).ToLowerInvariant();

                if (pkh != _orderBookScriptHash) return;

                OrderDatum orderDatum = CborSerializer.Deserialize<OrderDatum>(e.Output.Datum()!);
                AssetClass asset = orderDatum.Asset;

                string policyId = Convert.ToHexStringLower(asset.Value()[0].Value);
                string assetName = Convert.ToHexStringLower(asset.Value()[1].Value);

                OrderBySlot orderBySlotHistory = new(
                    txHash,
                    e.Index,
                    slot,
                    e.Output.Address()?.GetBaseAddressBech32()!,
                    policyId,
                    assetName,
                    orderDatum.Quantity.Value,
                    null,
                    null,
                    null,
                    tx.Raw ?? [],
                    e.Output.Datum(),
                    OrderStatus.Active
                );

                dbContext.OrdersBySlot.Add(orderBySlotHistory);
            });
    }

    private static void ProcessInputs(Block block, List<OrderBySlot> orderBySlotEntries, ArgusDbContext dbContext)
    {
        if (!orderBySlotEntries.Any()) return;

        List<TransactionBody> transactions = [.. block.TransactionBodies()];
        IEnumerable<(byte[]? RedeemerRaw, TransactionInput Input, TransactionBody Tx)> inputRedeemers = transactions.GetInputRedeemerTuple(block);
        ulong currentSlot = block.Slot() ?? 0;

        orderBySlotEntries.ForEach(entry =>
        {
            (byte[]? RedeemerRaw, TransactionInput Input, TransactionBody Tx) = inputRedeemers
                .FirstOrDefault(ir => ir.Input.TransactionId() == entry.TxHash && ir.Input.Index.Value == entry.Index);

            bool isSold = IsAcceptOrCancelRedeemer(entry, inputRedeemers);

            OrderBySlot? localEntry = dbContext.OrdersBySlot.Local
                .FirstOrDefault(e => e.TxHash == entry.TxHash && e.Index == entry.Index);

            Address? executorAddress = Tx.Outputs().Last().Address(); // TODO
            string executorAddressBech32 = executorAddress?.GetBaseAddressBech32() ?? string.Empty;

            OrderBySlot updatedEntry = entry with
            {
                SpentSlot = currentSlot,
                Status = isSold ? OrderStatus.Sold : OrderStatus.Cancelled,
                BuyerAddress = isSold ? executorAddressBech32 : null,
                SpentTxHash = isSold ? Tx.Id() : null
            };

            if (localEntry is not null)
                dbContext.Entry(localEntry).CurrentValues.SetValues(updatedEntry);
            else
                dbContext.Attach(updatedEntry).State = EntityState.Modified;
        });
    }

    public static bool IsAcceptOrCancelRedeemer(
        OrderBySlot order, 
        IEnumerable<(byte[]? RedeemerRaw, 
        TransactionInput Input, 
        TransactionBody Tx)> inputRedeemers
    )
    {
        // Get the input that spent this listing
        byte[]? redeemerRaw = inputRedeemers
            .Where(ir => ir.Input.TransactionId() == order.TxHash && ir.Input.Index() == order.Index)
            .Select(ir => ir.RedeemerRaw)
            .FirstOrDefault();

        if (redeemerRaw is null) return false;

        try
        {
            AcceptRedeemer? crashrBuyRedeemer = CborSerializer.Deserialize<AcceptRedeemer>(redeemerRaw);
            return true;
        }
        catch { }

        return false;
    }
}