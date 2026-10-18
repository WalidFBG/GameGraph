using System.Globalization;
using System.Text.Json;
using GameGraph.Engine;

namespace GameGraph.TrainingDataGeneration;

public sealed class TrainingDataWriter(string root, bool replace)
{
    private static readonly JsonSerializerOptions ManifestJson = new() { WriteIndented = true };

    public string Write<TState>(NodeSpec<TState> node, string source, IReadOnlyList<TrainingExample> examples, Dictionary<string, int> games)
    {
        var directory = Path.Combine(root, node.Name);
        var features = node.Features.Select(f => f.Name).ToArray();

        if (Directory.Exists(directory))
        {
            if (!replace)
                throw new IOException($"Training data already exists in {directory}; pass --replace to overwrite it");

            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);

        using (var writer = new StreamWriter(Path.Combine(directory, "examples.csv")))
        {
            writer.WriteLine(string.Join(',', [.. features, "outcome"]));

            foreach (var example in examples)
            {
                if (example.Features.Length != features.Length)
                    throw new InvalidDataException($"An example has {example.Features.Length} features, expected {features.Length}");

                var values = example.Features.Select(f => f.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteLine(string.Join(',', [.. values, example.Outcome.ToString(CultureInfo.InvariantCulture)]));
            }
        }

        var manifest = new TrainingDataManifest(node.Name, source, DateTime.UtcNow, features, examples.Count, games);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, ManifestJson));

        return directory;
    }
}
