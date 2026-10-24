namespace GameGraph.Models.Contract;

public sealed record FittedLinearTerm(string Feature, double Centre, double Scale, double[] Coefficients) : FittedTerm(Feature, Coefficients)
{
    public override Term Declaration() => new LinearTerm(Feature);
}
