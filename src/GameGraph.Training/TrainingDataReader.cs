using System.Globalization;
using System.Text.Json;

namespace GameGraph.Training;

public static class TrainingDataReader
{
    public static TrainingData Read(string directory)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));

        var node = manifest.RootElement.GetProperty("Node").GetString()
            ?? throw new InvalidDataException($"No node in the manifest in {directory}");
        var features = manifest.RootElement.GetProperty("Features").EnumerateArray()
            .Select(f => f.GetString() ?? throw new InvalidDataException($"A feature without a name in the manifest in {directory}"))
            .ToArray();
        var count = manifest.RootElement.GetProperty("Examples").GetInt32();

        var lines = File.ReadLines(Path.Combine(directory, "examples.csv")).GetEnumerator();
        var header = string.Join(',', [.. features, "outcome"]);

        if (!lines.MoveNext() || lines.Current != header)
            throw new InvalidDataException($"The header of examples.csv in {directory} is not {header}");

        var examples = new List<double[]>(count);
        var outcomes = new List<int>(count);

        while (lines.MoveNext())
        {
            var values = lines.Current.Split(',');

            if (values.Length != features.Length + 1)
                throw new InvalidDataException($"Example {examples.Count} in {directory} has {values.Length} columns, expected {features.Length + 1}");

            var outcome = int.Parse(values[^1], CultureInfo.InvariantCulture);

            if (outcome is not (0 or 1))
                throw new InvalidDataException($"Example {examples.Count} in {directory} has outcome {outcome}, expected 0 or 1");

            examples.Add(values[..^1].Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            outcomes.Add(outcome);
        }

        if (examples.Count != count)
            throw new InvalidDataException($"The manifest in {directory} says {count} examples, examples.csv has {examples.Count}");

        return new TrainingData(node, features, ReadGames(directory), examples.ToArray(), outcomes.ToArray());
    }

    private static TrainingGame[] ReadGames(string directory)
    {
        var lines = File.ReadLines(Path.Combine(directory, "games.csv")).GetEnumerator();
        const string header = "game,startUtc,examples";

        if (!lines.MoveNext() || lines.Current != header)
            throw new InvalidDataException($"The header of games.csv in {directory} is not {header}");

        var games = new List<TrainingGame>();
        var next = 0;

        while (lines.MoveNext())
        {
            var values = lines.Current.Split(',');

            if (values.Length != 3)
                throw new InvalidDataException($"Game {games.Count} in {directory} has {values.Length} columns, expected 3");

            var start = DateTime.Parse(values[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var examples = int.Parse(values[2], CultureInfo.InvariantCulture);

            games.Add(new TrainingGame(values[0], start, next, examples));
            next += examples;
        }

        return games.ToArray();
    }
}
