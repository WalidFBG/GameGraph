namespace GameGraph.Models.Contract;

public sealed record ChoiceDistribution : DistributionDeclaration
{
    public override PredictedParameter[] Predicted() => [new("utility", Link.Identity, HasIntercept: false)];

    public override string[] Given() => [];

    public override bool ChoosesAmongCandidates() => true;
}
