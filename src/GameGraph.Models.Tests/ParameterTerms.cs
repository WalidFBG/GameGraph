using GameGraph.Models.Contract;

namespace GameGraph.Models.Tests;

internal sealed class ParameterTerms : IEqualityComparer<Parameter>
{
    public static ParameterTerms Instance { get; } = new();

    public bool Equals(Parameter? x, Parameter? y) =>
        x is PredictedParameter a && y is PredictedParameter b ? a.Link == b.Link && a.Terms.SequenceEqual(b.Terms) : Equals((object?)x, y);

    public int GetHashCode(Parameter obj) => obj.GetHashCode();
}
