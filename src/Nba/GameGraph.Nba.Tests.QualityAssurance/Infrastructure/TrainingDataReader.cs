using System.Globalization;
using GameGraph.Models.Contract;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class TrainingDataReader
{
    public static (TrainingDataManifest Manifest, ExampleTable Examples) Read(string node)
    {
        var directory = DataDirectories.TrainingData(node);
        var manifest = ContractFiles.ReadTrainingDataManifest(Path.Combine(directory, "manifest.json"));
        var games = GameRows(directory).Select(r => int.Parse(r[2], CultureInfo.InvariantCulture)).ToArray();
        var lines = Lines(Path.Combine(directory, "examples.csv"), [.. manifest.Features, "outcome"]);

        if (lines.Length != manifest.Examples || games.Sum() != manifest.Examples)
            throw new InvalidDataException($"{directory} has {lines.Length} examples in {games.Sum()} game rows, the manifest says {manifest.Examples}");

        var features = new double[lines.Length][];
        var outcomes = new int[lines.Length];

        for (var e = 0; e < lines.Length; e++)
        {
            var values = lines[e].Split(',');

            if (values.Length != manifest.Features.Length + 1)
                throw new InvalidDataException($"Example {e} in {directory} does not have {manifest.Features.Length + 1} columns");

            features[e] = [.. values[..^1].Select(v => double.Parse(v, CultureInfo.InvariantCulture))];
            outcomes[e] = int.Parse(values[^1], CultureInfo.InvariantCulture);
        }

        var gameOfExample = games.SelectMany((count, game) => Enumerable.Repeat(game, count)).ToArray();

        return (manifest, new ExampleTable(manifest.Features, gameOfExample, features, outcomes));
    }

    public static string[] GameIds(string node) => [.. GameRows(DataDirectories.TrainingData(node)).Select(r => r[0])];

    private static string[][] GameRows(string directory) =>
        [.. Lines(Path.Combine(directory, "games.csv"), ["game", "startUtc", "examples"]).Select(l => l.Split(','))];

    private static string[] Lines(string path, string[] header)
    {
        var lines = File.ReadAllLines(path);

        if (lines.Length == 0 || lines[0] != string.Join(',', header))
            throw new InvalidDataException($"The header of {path} is not {string.Join(',', header)}");

        return lines[1..];
    }
}
