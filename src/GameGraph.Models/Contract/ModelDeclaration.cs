namespace GameGraph.Models.Contract;

public sealed record ModelDeclaration(Distribution Distribution, IReadOnlyDictionary<string, PredictedParameter> Parameters)
{
    public IEnumerable<string> Features() => Parameters.Values.SelectMany(p => p.Terms).Select(t => t.Feature).Distinct();
}
