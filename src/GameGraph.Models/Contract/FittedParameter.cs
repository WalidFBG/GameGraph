using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "source")]
[JsonDerivedType(typeof(FittedPredictedParameter), "predicted")]
[JsonDerivedType(typeof(FittedGivenParameter), "given")]
public abstract record FittedParameter
{
    public abstract Parameter Declaration();
}
