using System.Text.Json.Serialization;
using System.Text.Json;
using GameGraph.Contract.Distributions;
using GameGraph.Contract.Terms;

namespace GameGraph.Contract.Files;

internal sealed class FittedModelJson : JsonConverter<FittedModel>
{
    public override FittedModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var model = root.GetProperty("model").Deserialize<ModelDeclaration>(options) ?? throw new JsonException("A fitted model has no model");
        var parameters = model.Distribution.Predicted().ToDictionary(p => p.Name);
        var fit = root.GetProperty("fit").EnumerateObject().ToDictionary(
            p => p.Name,
            p => ReadPredictorFit(Declared(parameters, p.Name, "predicted parameter"), Declared(model.Terms, p.Name, "predicted parameter"), p.Value, options));

        return new FittedModel(root.GetProperty("node").GetString() ?? throw new JsonException("A fitted model has no node"), model, fit);
    }

    public override void Write(Utf8JsonWriter writer, FittedModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("node", value.Node);
        writer.WritePropertyName("model");
        JsonSerializer.Serialize(writer, value.Model, options);
        writer.WriteStartObject("fit");

        var parameters = value.Model.Distribution.Predicted().ToDictionary(p => p.Name);

        foreach (var (name, predictor) in value.Fit)
        {
            writer.WriteStartObject(name);

            if (parameters[name].HasIntercept)
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

    private static PredictorFit ReadPredictorFit(PredictedParameter parameter, Term[] terms, JsonElement element, JsonSerializerOptions options)
    {
        var declared = terms.ToDictionary(t => t.Feature);
        var fits = element.GetProperty("terms").EnumerateObject().ToDictionary(
            p => p.Name,
            p => ReadTermFit(Declared(declared, p.Name, "term"), p.Value, options));
        var hasIntercept = element.TryGetProperty("intercept", out var intercept);

        if (hasIntercept != parameter.HasIntercept)
            throw new ArgumentException($"The fit of {parameter.Name} {(hasIntercept ? "has" : "has no")} intercept, but the parameter {(parameter.HasIntercept ? "has one" : "has none")}", nameof(element));

        return new PredictorFit(hasIntercept ? intercept.GetDouble() : 0, fits);
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
