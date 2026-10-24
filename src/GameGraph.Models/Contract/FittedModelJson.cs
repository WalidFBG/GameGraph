using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

internal sealed class FittedModelJson : JsonConverter<FittedModel>
{
    public override FittedModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var model = root.GetProperty("model").Deserialize<ModelDeclaration>(options) ?? throw new JsonException("A fitted model has no model");
        var fit = root.GetProperty("fit").EnumerateObject().ToDictionary(
            p => p.Name,
            p => ReadPredictorFit(Declared(model.Terms, p.Name, "predicted parameter"), p.Value, options));

        return new FittedModel(root.GetProperty("node").GetString() ?? throw new JsonException("A fitted model has no node"), model, fit);
    }

    public override void Write(Utf8JsonWriter writer, FittedModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("node", value.Node);
        writer.WritePropertyName("model");
        JsonSerializer.Serialize(writer, value.Model, options);
        writer.WriteStartObject("fit");

        foreach (var (name, predictor) in value.Fit)
        {
            writer.WriteStartObject(name);
            writer.WriteNumber("intercept", predictor.Intercept);
            writer.WriteStartObject("terms");

            foreach (var (feature, fit) in predictor.Terms)
            {
                writer.WritePropertyName(feature);
                JsonSerializer.Serialize(writer, fit, fit.GetType(), options);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static PredictorFit ReadPredictorFit(Term[] terms, JsonElement element, JsonSerializerOptions options)
    {
        var declared = terms.ToDictionary(t => t.Feature);
        var fits = element.GetProperty("terms").EnumerateObject().ToDictionary(
            p => p.Name,
            p => ReadTermFit(Declared(declared, p.Name, "term"), p.Value, options));

        return new PredictorFit(element.GetProperty("intercept").GetDouble(), fits);
    }

    private static TermFit ReadTermFit(Term term, JsonElement element, JsonSerializerOptions options) => term switch
    {
        LinearTerm => (TermFit?)element.Deserialize<LinearFit>(options),
        LinearSplineTerm => element.Deserialize<LinearSplineFit>(options),
        _ => throw new ArgumentOutOfRangeException(nameof(term), term.GetType().Name, "Not a term"),
    } ?? throw new JsonException($"The fit of {term.Feature} is empty");

    private static T Declared<T>(IReadOnlyDictionary<string, T> declared, string name, string kind) =>
        declared.TryGetValue(name, out var value) ? value : throw new ArgumentException($"{name} is not a declared {kind}", nameof(name));
}
