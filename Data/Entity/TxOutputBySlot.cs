using Argus.Sync.Data.Models;
using Argus.Sync.Data.Models.Enums;

namespace Argus.Example.Data.Entity;

public record TxOutputBySlot(
    string TxHash,
    ulong Index,
    string OwnerAddress,
    ulong Slot,
    ulong? SpentSlot,
    byte[] RawData,
    byte[]? DatumRaw,
    byte[] AmountRaw,
    UtxoStatus Status
) : IReducerModel;