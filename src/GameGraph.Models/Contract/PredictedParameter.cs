namespace GameGraph.Models.Contract;

public sealed record PredictedParameter : Parameter
{
    public PredictedParameter(Link link, Term[] terms)
    {
        DistinctNames.Require(terms.Select(t => t.Feature));

        Link = link;
        Terms = terms;
    }

    public Link Link { get; }

    public Term[] Terms { get; }

    public override IEnumerable<string> Features() => Terms.Select(t => t.Feature);
}
