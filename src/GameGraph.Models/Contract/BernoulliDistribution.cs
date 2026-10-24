namespace GameGraph.Models.Contract;

public sealed record BernoulliDistribution : DistributionDeclaration
{
    public override string[] ParameterNames() => ["probability"];
}
