using Argus.Example.Data.Enum;
using Argus.Sync.Data.Models;

namespace Argus.Example.Data.Entity;

public record OrderBySlot(
    string TxHash,
    ulong Index,
    ulong Slot,
    string OwnerAddress,
    string PolicyId,
    string AssetName,
    ulong Quantity,
    ulong? SpentSlot,
    string? BuyerAddress,
    string? SpentTxHash,
    byte[] RawData,
    byte[]? DatumRaw,
    OrderStatus Status
) : IReducerModel;