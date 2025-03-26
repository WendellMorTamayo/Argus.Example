using Chrysalis.Cardano.Sundae.Types.Common;
using Chrysalis.Cbor.Attributes;
using Chrysalis.Cbor.Converters.Primitives;
using Chrysalis.Cbor.Types;
using Chrysalis.Cbor.Types.Primitives;

namespace Argus.Example.Data.Cbor.Datums;

[CborConverter(typeof(ConstrConverter))]
[CborIndex(0)]
public record OrderDatum(
    [CborProperty(0)]
    CborBytes Owner,

    [CborProperty(1)]
    AssetClass Asset,

    [CborProperty(2)]
    CborUlong Quantity
) : CborBase;