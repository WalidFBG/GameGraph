using System.Globalization;
using GameGraph.Engine;
using GameGraph.Models.Contract;

namespace GameGraph.TrainingDataGeneration;

public sealed class TrainingDataWriter(string root)
{
    public string Write<TState>(NodeSpec<TState> node, string source, IReadOnlyList<GameExamples> games)
    {
        var directory = Path.Combine(root, node.Name);
        var features = node.FeatureNames;
        var choice = node.Model.Distribution.ChoosesAmongCandidates();

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);

        Directory.CreateDirectory(directory);

        using (var writer = new StreamWriter(Path.Combine(directory, "examples.csv")))
        {
            writer.WriteLine(string.Join(',', choice ? [TrainingDataFiles.Decision, .. features, TrainingDataFiles.Outcome] : (string[])[.. features, TrainingDataFiles.Outcome]));

            var decision = 0;

            foreach (var example in games.SelectMany(g => g.Examples))
            {
                var rows = choice ? example.Features.Length / features.Length : 1;

                if (example.Features.Length != rows * features.Length || rows == 0)
                    throw new InvalidDataException($"An example has {example.Features.Length} feature values, not {(choice ? "one or more candidates" : "one row")} of {features.Length} features");

                for (var r = 0; r < rows; r++)
                {
                    var values = example.Features.Skip(r * features.Length).Take(features.Length).Select(f => f.ToString("R", CultureInfo.InvariantCulture));
                    var outcome = choice ? (r == example.Outcome ? 1 : 0) : example.Outcome;
                    string[] prefix = choice ? [decision.ToString(CultureInfo.InvariantCulture)] : [];

                    writer.WriteLine(string.Join(',', [.. prefix, .. values, outcome.ToString(CultureInfo.InvariantCulture)]));
                }

                decision++;
            }
        }

        using (var writer = new StreamWriter(Path.Combine(directory, "games.csv")))
        {
            writer.WriteLine(string.Join(',', TrainingDataFiles.GamesHeader));

            foreach (var game in games)
                writer.WriteLine($"{game.GameId},{game.StartUtc.ToString("O", CultureInfo.InvariantCulture)},{game.Examples.Count.ToString(CultureInfo.InvariantCulture)}");
        }

        var manifest = new TrainingDataManifest(node.Name, source, DateTime.UtcNow, features, games.Sum(g => g.Examples.Count), node.Model);
        ContractFiles.WriteTrainingDataManifest(Path.Combine(directory, "manifest.json"), manifest);

        return directory;
    }
}
