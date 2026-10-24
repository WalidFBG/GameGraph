using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.Schema;

namespace GameGraph.Models.Contract;

public static class ContractFiles
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true,
    };

    private static readonly SchemaRegistry Registry = RegistryWithDefinitions();
    private static readonly DocumentSchema TrainingDataManifestSchema = DocumentSchema.Load("training-data-manifest.schema.json");
    private static readonly DocumentSchema FittedModelSchema = DocumentSchema.Load("fitted-model.schema.json");
    private static readonly DocumentSchema TrainingReportSchema = DocumentSchema.Load("training-report.schema.json");

    public static TrainingDataManifest ReadTrainingDataManifest(string path) => Read<TrainingDataManifest>(path, TrainingDataManifestSchema);

    public static void WriteTrainingDataManifest(string path, TrainingDataManifest manifest) => Write(path, manifest, TrainingDataManifestSchema);

    public static FittedModel ReadFittedModel(string path) => Read<FittedModel>(path, FittedModelSchema);

    public static void WriteFittedModel(string path, FittedModel model) => Write(path, model, FittedModelSchema);

    public static TrainingReport ReadTrainingReport(string path) => Read<TrainingReport>(path, TrainingReportSchema);

    public static void WriteTrainingReport(string path, TrainingReport report) => Write(path, report, TrainingReportSchema);

    private static T Read<T>(string path, DocumentSchema schema)
    {
        var document = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException($"{path} is empty");

        schema.Validate(document, path);
        document.Remove("schemaVersion");

        return document.Deserialize<T>(Json) ?? throw new InvalidDataException($"{path} is empty");
    }

    private static void Write<T>(string path, T value, DocumentSchema schema)
    {
        var document = JsonSerializer.SerializeToNode(value, Json)!.AsObject();

        document.Insert(0, "schemaVersion", SchemaVersion);
        schema.Validate(document, path);

        File.WriteAllText(path, document.ToJsonString(Json) + "\n");
    }

    private static SchemaRegistry RegistryWithDefinitions()
    {
        var registry = new SchemaRegistry();

        JsonSchema.FromText(Embedded("model.schema.json"), new BuildOptions { SchemaRegistry = registry });

        return registry;
    }

    private static string Embedded(string name)
    {
        using var stream = typeof(ContractFiles).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The schema {name} is not embedded");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private sealed record DocumentSchema(string Name, JsonSchema Schema)
    {
        public static DocumentSchema Load(string name) =>
            new(name, JsonSchema.FromText(Embedded(name), new BuildOptions { SchemaRegistry = Registry }));

        public void Validate(JsonObject document, string path)
        {
            var results = Schema.Evaluate(JsonSerializer.SerializeToElement(document), new EvaluationOptions { OutputFormat = OutputFormat.List });

            if (results.IsValid)
                return;

            var failure = results.Details?.FirstOrDefault(d => d.Errors is { Count: > 0 })
                ?? throw new InvalidDataException($"{path} does not match {Name}");

            throw new InvalidDataException($"{path} does not match {Name}: {failure.Errors!.Values.First()} at {failure.InstanceLocation}");
        }
    }
}
