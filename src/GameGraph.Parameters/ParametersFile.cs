using System.Text.Json;

namespace GameGraph.Parameters;

public static class ParametersFile
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Write(string directory, NodeParameters parameters)
    {
        var path = PathOf(directory, parameters.Node);

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(parameters, Json));

        return path;
    }

    public static NodeParameters Read(string directory, string node)
    {
        var path = PathOf(directory, node);
        var parameters = JsonSerializer.Deserialize<NodeParameters>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException($"{path} is empty");

        if (parameters.Node != node)
            throw new InvalidDataException($"{path} holds the parameters of {parameters.Node}, not {node}");

        if (parameters.Family is null)
            throw new InvalidDataException($"{path} has no family");

        if (parameters.Predictors is null || parameters.Predictors.Length == 0)
            throw new InvalidDataException($"{path} has no predictors");

        if (parameters.Predictors.Any(p => p.Terms is null || p.Terms.Any(t => t.Feature is null || t.Basis is null || t.Coefficients is null)))
            throw new InvalidDataException($"{path} has an incomplete term");

        return parameters;
    }

    private static string PathOf(string directory, string node) => Path.Combine(directory, $"{node}.json");
}
