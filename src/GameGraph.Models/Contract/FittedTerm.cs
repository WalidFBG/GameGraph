using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "basis")]
[JsonDerivedType(typeof(FittedLinearTerm), "linear")]
[JsonDerivedType(typeof(FittedLinearSplineTerm), "linearSpline")]
public abstract record FittedTerm(string Feature, double[] Coefficients)
{
    public abstract Term Declaration();
}
