using System.Globalization;
using GameGraph.Engine;
using GameGraph.Models.Contract;

namespace GameGraph.TrainingDataGeneration;

public sealed class TrainingDataWriter(string root)
{
    public string Write<TState, TIncidentCollector>(NodeSpec<TState, TIncidentCollector> node, string source, IReadOnlyList<GameExamples> games)
    {
        var directory = Path.Combine(root, node.Name);
        var features = node.Features.Select(f => f.Name).ToArray();

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);

        Directory.CreateDirectory(directory);

        using (var writer = new StreamWriter(Path.Combine(directory, "examples.csv")))
        {
            writer.WriteLine(string.Join(',', [.. features, "outcome"]));

            foreach (var example in games.SelectMany(g => g.Examples))
            {
                if (example.Features.Length != features.Length)
                    throw new InvalidDataException($"An example has {example.Features.Length} features, expected {features.Length}");

                var values = example.Features.Select(f => f.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteLine(string.Join(',', [.. values, example.Outcome.ToString(CultureInfo.InvariantCulture)]));
            }
        }

        using (var writer = new StreamWriter(Path.Combine(directory, "games.csv")))
        {
            writer.WriteLine("game,startUtc,examples");

            foreach (var game in games)
                writer.WriteLine($"{game.GameId},{game.StartUtc.ToString("O", CultureInfo.InvariantCulture)},{game.Examples.Count.ToString(CultureInfo.InvariantCulture)}");
        }

        var manifest = new TrainingDataManifest(node.Name, source, DateTime.UtcNow, features, games.Sum(g => g.Examples.Count), node.Model);
        ContractFiles.WriteTrainingDataManifest(Path.Combine(directory, "manifest.json"), manifest);

        return directory;
    }
}
