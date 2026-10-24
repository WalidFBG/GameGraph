using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

public sealed record FittedPredictedParameter
{
    public FittedPredictedParameter(Link link, double intercept, FittedLinearTerm[] terms)
    {
        DistinctNames.Require(terms.Select(t => t.Feature));

        Link = link;
        Intercept = intercept;
        Terms = terms;
    }

    [JsonPropertyOrder(-1)]
    public string Source => "predicted";

    public Link Link { get; }

    public double Intercept { get; }

    public FittedLinearTerm[] Terms { get; }

    public PredictedParameter Declaration() => new(Link, Terms.Select(t => t.Declaration()).ToArray());
}
