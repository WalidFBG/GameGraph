using System.Globalization;
using GameGraph.Models.Contract;

namespace GameGraph.Models.Tests.Unit;

internal sealed record TrainingData(TrainingDataManifest Manifest, int[] GameExamples, double[][] Features, int[] Outcomes)
{
    public int ExamplesIn(int games) => GameExamples.Take(games).Sum();
}

internal static class TrainingDataFiles
{
    public static TrainingData Read(string directory)
    {
        var manifest = ContractFiles.ReadTrainingDataManifest(Path.Combine(directory, "manifest.json"));
        var rows = Rows(Path.Combine(directory, "examples.csv"), [.. manifest.Features, "outcome"]);
        var games = Rows(Path.Combine(directory, "games.csv"), ["game", "startUtc", "examples"]).Select(r => int.Parse(r[2], CultureInfo.InvariantCulture)).ToArray();

        if (rows.Length != manifest.Examples || games.Sum() != manifest.Examples)
            throw new InvalidDataException($"{directory} has {rows.Length} examples in {games.Sum()} game rows, the manifest says {manifest.Examples}");

        return new TrainingData(
            manifest,
            games,
            rows.Select(r => r[..^1].Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToArray(),
            rows.Select(r => int.Parse(r[^1], CultureInfo.InvariantCulture)).ToArray());
    }

    private static string[][] Rows(string path, string[] header)
    {
        var lines = File.ReadAllLines(path);

        if (lines.Length == 0 || lines[0] != string.Join(',', header))
            throw new InvalidDataException($"The header of {path} is not {string.Join(',', header)}");

        var rows = lines[1..].Select(l => l.Split(',')).ToArray();

        if (rows.Any(r => r.Length != header.Length))
            throw new InvalidDataException($"{path} has a row without {header.Length} columns");

        return rows;
    }
}
