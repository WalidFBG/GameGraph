using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

public sealed record LinearTerm(string Feature)
{
    [JsonPropertyOrder(-1)]
    public string Basis => "linear";
}
