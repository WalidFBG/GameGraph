namespace GameGraph.Contract.Distributions;

public sealed record ChoiceDistribution : DistributionDeclaration
{
    public const string Size = "size";
    public const int MostCandidates = 31;

    public override PredictedParameter[] Predicted() => [new("utility", Link.Identity, HasIntercept: false)];

    public override string[] Given() => [Size];

    public override bool ChoosesAmongCandidates() => true;
}
