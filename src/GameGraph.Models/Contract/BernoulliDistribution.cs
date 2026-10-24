namespace GameGraph.Models.Contract;

public sealed record BernoulliDistribution : DistributionDeclaration
{
    public override PredictedParameter[] Predicted() => [new("probability", Link.Logit)];

    public override string[] Given() => [];
}
