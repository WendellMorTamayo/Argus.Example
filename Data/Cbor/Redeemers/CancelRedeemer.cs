using Chrysalis.Cbor.Attributes;
using Chrysalis.Cbor.Converters.Primitives;
using Chrysalis.Cbor.Types;

namespace Argus.Example.Data.Cbor.Redeemers;

[CborConverter(typeof(ConstrConverter))]
[CborIndex(1)]
public record CancelRedeemer() : CborBase;