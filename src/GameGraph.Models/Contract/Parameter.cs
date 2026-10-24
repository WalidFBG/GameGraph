using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "source")]
[JsonDerivedType(typeof(PredictedParameter), "predicted")]
[JsonDerivedType(typeof(GivenParameter), "given")]
public abstract record Parameter
{
    public abstract IEnumerable<string> Features();
}
