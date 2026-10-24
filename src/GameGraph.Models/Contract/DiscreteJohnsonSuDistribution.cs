namespace GameGraph.Models.Contract;

public sealed record DiscreteJohnsonSuDistribution : DistributionDeclaration
{
    public override string[] ParameterNames() => ["location", "scale", "skew", "tail", "maximum"];
}
