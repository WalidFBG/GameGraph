namespace GameGraph.Models.Contract;

public sealed record FittedModel(string Node, Distribution Distribution, IReadOnlyDictionary<string, FittedPredictedParameter> Parameters)
{
    public ModelDeclaration Declaration() => new(Distribution, Parameters.ToDictionary(p => p.Key, p => p.Value.Declaration()));
}
