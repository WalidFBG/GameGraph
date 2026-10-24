namespace GameGraph.Models.Contract;

public sealed record FittedPredictedParameter : FittedParameter
{
    public FittedPredictedParameter(Link link, double intercept, FittedTerm[] terms)
    {
        DistinctNames.Require(terms.Select(t => t.Feature));

        Link = link;
        Intercept = intercept;
        Terms = terms;
    }

    public Link Link { get; }

    public double Intercept { get; }

    public FittedTerm[] Terms { get; }

    public override Parameter Declaration() => new PredictedParameter(Link, Terms.Select(t => t.Declaration()).ToArray());
}
