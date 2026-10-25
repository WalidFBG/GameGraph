namespace GameGraph.Models.Contract;

public sealed record BernoulliDistribution : DistributionDeclaration
{
    public override PredictedParameter[] Predicted() => [new("probability", Link.Logit, HasIntercept: true)];

    public override string[] Given() => [];

    public override bool ChoosesAmongCandidates() => false;
}
