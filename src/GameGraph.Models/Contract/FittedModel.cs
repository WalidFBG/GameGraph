namespace GameGraph.Models.Contract;

public sealed record FittedModel
{
    public FittedModel(string node, DistributionDeclaration distribution, IReadOnlyDictionary<string, FittedParameter> parameters)
    {
        distribution.RequireParameters(parameters.Keys);

        Node = node;
        Distribution = distribution;
        Parameters = parameters;
    }

    public string Node { get; }

    public DistributionDeclaration Distribution { get; }

    public IReadOnlyDictionary<string, FittedParameter> Parameters { get; }

    public ModelDeclaration Declaration() => new(Distribution, Parameters.ToDictionary(p => p.Key, p => p.Value.Declaration()));
}
