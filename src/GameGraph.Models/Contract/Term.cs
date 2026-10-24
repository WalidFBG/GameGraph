using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "basis")]
[JsonDerivedType(typeof(LinearTerm), "linear")]
[JsonDerivedType(typeof(LinearSplineTerm), "linearSpline")]
public abstract record Term(string Feature);
