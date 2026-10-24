namespace GameGraph.Models.Contract;

public sealed record FittedPredictor
{
    public FittedPredictor(double intercept, FittedTerm[] terms)
    {
        DistinctNames.Require(terms.Select(t => t.Feature));

        Intercept = intercept;
        Terms = terms;
    }

    public double Intercept { get; }

    public FittedTerm[] Terms { get; }

    public Term[] Declaration() => Terms.Select(t => t.Declaration()).ToArray();
}
