using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

public sealed record PredictedParameter
{
    public PredictedParameter(Link link, LinearTerm[] terms)
    {
        DistinctNames.Require(terms.Select(t => t.Feature));

        Link = link;
        Terms = terms;
    }

    [JsonPropertyOrder(-1)]
    public string Source => "predicted";

    public Link Link { get; }

    public LinearTerm[] Terms { get; }
}
