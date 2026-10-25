namespace GameGraph.Models.Contract;

public sealed record DiscreteJohnsonSuDistribution : DistributionDeclaration
{
    public override PredictedParameter[] Predicted() =>
        [new("location", Link.Identity, HasIntercept: true), new("scale", Link.Log, HasIntercept: true), new("skew", Link.Identity, HasIntercept: true), new("tail", Link.Log, HasIntercept: true)];

    public override string[] Given() => ["maximum"];

    public override bool ChoosesAmongCandidates() => false;
}
