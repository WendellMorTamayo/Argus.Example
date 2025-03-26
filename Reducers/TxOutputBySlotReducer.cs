using System.Linq.Expressions;
using Argus.Example.Data;
using Argus.Example.Data.Entity;
using Argus.Example.Data.Extensions;
using Argus.Sync.Data.Models.Enums;
using Argus.Sync.Extensions;
using Argus.Sync.Reducers;
using Chrysalis.Cardano.Core.Extensions;
using Chrysalis.Cardano.Core.Types.Block;
using Chrysalis.Cardano.Core.Types.Block.Transaction.Body;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Argus.Example.Reducers;

public class TxOutputBySlotReducer(IDbContextFactory<ArgusDbContext> dbContextFactory) : IReducer<TxOutputBySlot>
{
    public async Task RollBackwardAsync(ulong slot)
    {
        await using ArgusDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        List<TxOutputBySlot> spentOutputs = await dbContext.TxOutputsBySlot
            .Where(o => o.SpentSlot >= slot)
            .ToListAsync();

        if (!spentOutputs.Any()) return;

        List<TxOutputBySlot> updatedEntries = [.. spentOutputs.Select(existing => existing with
        {
            Status = UtxoStatus.Unspent,
            SpentSlot = null
        })];

        foreach (TxOutputBySlot existing in spentOutputs)
        {
            EntityEntry<TxOutputBySlot>? trackedEntity = dbContext.ChangeTracker.Entries<TxOutputBySlot>()
                .FirstOrDefault(e =>
                    e.Entity.Slot == existing.Slot &&
                    e.Entity.TxHash == existing.TxHash &&
                    e.Entity.Index == existing.Index);

            if (trackedEntity is not null)
            {
                trackedEntity.State = EntityState.Detached;
            }
        }

        dbContext.TxOutputsBySlot.UpdateRange(updatedEntries);
        dbContext.TxOutputsBySlot.RemoveRange(
            dbContext.TxOutputsBySlot
            .AsNoTracking()
            .Where(o => o.Slot >= slot && o.SpentSlot == null)
        );

        await dbContext.SaveChangesAsync();
    }

    public async Task RollForwardAsync(Block block)
    {
        await using ArgusDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        IEnumerable<TransactionBody> transactions = block.TransactionBodies();
        if (!transactions.Any()) return;

        transactions.ToList().ForEach(transaction => ProcessOutputs(
            transaction,
            dbContext,
            block
        ));

        List<(string txHash, ulong index)> inputOutRefs = [.. block.TransactionBodies()
            .SelectMany(
                txBody =>
                    txBody.Inputs()
                        .Select(input => (input.TransactionId(), input.Index()))
            )];

        Expression<Func<TxOutputBySlot, bool>> predicate = PredicateBuilder.False<TxOutputBySlot>();
        inputOutRefs.ForEach(input =>
            predicate = predicate.Or(o => o.TxHash == input.txHash && o.Index == input.index)
        );

        List<TxOutputBySlot> existingEntries = await dbContext.TxOutputsBySlot
            .Where(predicate)
            .ToListAsync();

        transactions.ToList().ForEach(transaction => ProcessInputs(
            existingEntries,
            dbContext,
            block
        ));

        await dbContext.SaveChangesAsync();
    }

    private static void ProcessOutputs(TransactionBody tx, ArgusDbContext dbContext, Block block)
    {
        ulong slot = block.Slot() ?? 0;
        string txHash = tx.Id();

        tx.Outputs().Select((output, index) => new { Output = output, Index = (ulong)index })
            .ToList().ForEach(e =>
            {
                TxOutputBySlot newEntry = new(
                    txHash,
                    e.Index,
                    e.Output.Address()?.GetBaseAddressBech32() ?? string.Empty,
                    slot,
                    null,
                    e.Output.Raw ?? [],
                    e.Output.Datum(),
                    e.Output.Amount()?.Raw ?? [],
                    UtxoStatus.Unspent
                );

                dbContext.TxOutputsBySlot.Add(newEntry);
            });
    }

    private static void ProcessInputs(List<TxOutputBySlot> existingEntries, ArgusDbContext dbContext, Block block)
    {
        if (!existingEntries.Any()) return;

        ulong slot = block.Slot() ?? 0;

        foreach (TxOutputBySlot existing in existingEntries)
        {
            TxOutputBySlot updatedEntry = existing with
            {
                Status = UtxoStatus.Spent,
                SpentSlot = slot
            };

            EntityEntry<TxOutputBySlot>? trackedEntity = dbContext.ChangeTracker.Entries<TxOutputBySlot>()
                .FirstOrDefault(e =>
                    e.Entity.Slot == existing.Slot &&
                    e.Entity.TxHash == existing.TxHash &&
                    e.Entity.Index == existing.Index);

            if (trackedEntity is not null)
            {
                trackedEntity.State = EntityState.Detached;
            }

            dbContext.Entry(updatedEntry).State = EntityState.Modified;
        }
    }
}