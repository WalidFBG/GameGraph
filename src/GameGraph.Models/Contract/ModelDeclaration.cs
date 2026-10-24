namespace GameGraph.Models.Contract;

public sealed record ModelDeclaration
{
    public ModelDeclaration(DistributionDeclaration distribution, IReadOnlyDictionary<string, Parameter> parameters)
    {
        distribution.RequireParameters(parameters.Keys);

        Distribution = distribution;
        Parameters = parameters;
    }

    public DistributionDeclaration Distribution { get; }

    public IReadOnlyDictionary<string, Parameter> Parameters { get; }

    public IEnumerable<string> Features() => Parameters.Values.SelectMany(p => p.Features()).Distinct();
}
