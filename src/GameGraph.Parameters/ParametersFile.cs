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

        if (parameters.Weights is null)
            throw new InvalidDataException($"{path} has no weights");

        return parameters;
    }

    private static string PathOf(string directory, string node) => Path.Combine(directory, $"{node}.json");
}
