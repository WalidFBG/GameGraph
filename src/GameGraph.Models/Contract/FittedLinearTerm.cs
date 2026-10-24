using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

public sealed record FittedLinearTerm(string Feature, double Centre, double Scale, double[] Coefficients)
{
    [JsonPropertyOrder(-1)]
    public string Basis => "linear";

    public LinearTerm Declaration() => new(Feature);
}
