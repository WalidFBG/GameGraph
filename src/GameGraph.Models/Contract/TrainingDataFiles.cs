using System.Globalization;

namespace GameGraph.Models.Contract;

public static class TrainingDataFiles
{
    public const string Decision = "decision";
    public const string Outcome = "outcome";
    public static readonly string[] GamesHeader = ["game", "startUtc", "examples"];

    public static TrainingDataFile Read(string directory)
    {
        var manifest = ContractFiles.ReadTrainingDataManifest(Path.Combine(directory, "manifest.json"));
        var games = Rows(Path.Combine(directory, "games.csv"), GamesHeader);
        var choice = manifest.Model.Distribution.ChoosesAmongCandidates();
        var header = choice ? [Decision, .. manifest.Features, Outcome] : (string[])[.. manifest.Features, Outcome];
        var rows = Rows(Path.Combine(directory, "examples.csv"), header);
        var (features, outcomes) = choice ? Choices(directory, rows) : ([.. rows.Select(r => Numbers(r[..^1]))], [.. rows.Select(r => Whole(r[^1]))]);
        var examples = games.Select(r => Whole(r[2])).ToArray();

        if (outcomes.Length != manifest.Examples || examples.Sum() != manifest.Examples)
            throw new InvalidDataException($"{directory} has {outcomes.Length} examples in {examples.Sum()} game rows, the manifest says {manifest.Examples}");

        return new TrainingDataFile(manifest, [.. games.Select(r => r[0])], examples, features, outcomes);
    }

    public static string[] GameIds(string directory) => [.. Rows(Path.Combine(directory, "games.csv"), GamesHeader).Select(r => r[0])];

    private static (double[][] Features, int[] Outcomes) Choices(string directory, string[][] rows)
    {
        var decisions = rows.GroupBy(r => Whole(r[0])).ToArray();

        if (decisions.Select(d => d.Key).Where((decision, index) => decision != index).Any())
            throw new InvalidDataException($"{directory} does not number its decisions from 0 in order");

        var features = decisions.Select(d => d.SelectMany(r => Numbers(r[1..^1])).ToArray()).ToArray();
        var outcomes = decisions.Select(d => Chosen(directory, d.Key, [.. d.Select(r => Whole(r[^1]))])).ToArray();

        return (features, outcomes);
    }

    private static int Chosen(string directory, int decision, int[] chosen)
    {
        if (chosen.Any(c => c is not (0 or 1)) || chosen.Sum() != 1)
            throw new InvalidDataException($"Decision {decision} in {directory} does not choose exactly one candidate");

        return Array.IndexOf(chosen, 1);
    }

    private static double[] Numbers(string[] values) => [.. values.Select(v => double.Parse(v, CultureInfo.InvariantCulture))];

    private static int Whole(string value) => int.Parse(value, CultureInfo.InvariantCulture);

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
