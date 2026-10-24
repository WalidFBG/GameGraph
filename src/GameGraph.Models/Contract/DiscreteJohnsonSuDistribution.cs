namespace GameGraph.Models.Contract;

public sealed record DiscreteJohnsonSuDistribution : DistributionDeclaration
{
    public override PredictedParameter[] Predicted() =>
        [new("location", Link.Identity), new("scale", Link.Log), new("skew", Link.Identity), new("tail", Link.Log)];

    public override string[] Given() => ["maximum"];
}
