using System.Globalization;

namespace GameGraph.Contract.Files;

public static class TrainingDataFiles
{
    private const string Manifest = "manifest.json";
    private const string Examples = "examples.csv";
    private const string Games = "games.csv";
    private const string Decision = "decision";
    private const string Outcome = "outcome";
    private static readonly string[] GamesHeader = ["game", "startUtc", "examples"];

    public static TrainingData Read(string directory)
    {
        var manifest = ContractFiles.ReadTrainingDataManifest(Path.Combine(directory, Manifest));
        var games = Rows(Path.Combine(directory, Games), GamesHeader);
        var rows = Rows(Path.Combine(directory, Examples), ExamplesHeader(manifest));
        var examples = Choosing(manifest) ? Choices(directory, rows) : [.. rows.Select(r => new TrainingExample(Numbers(r[..^1]), Whole(r[^1])))];
        var counts = games.Select(r => Whole(r[2])).ToArray();

        if (examples.Length != counts.Sum())
            throw new InvalidDataException($"{directory} has {examples.Length} examples, its game rows say {counts.Sum()}");

        var gameExamples = new GameExamples[games.Length];
        var start = 0;

        for (var g = 0; g < games.Length; g++)
        {
            gameExamples[g] = new GameExamples(games[g][0], StartUtc(games[g][1]), examples[start..(start + counts[g])]);
            start += counts[g];
        }

        return new TrainingData(manifest, gameExamples);
    }

    public static void Write(string directory, TrainingData data)
    {
        var manifest = data.Manifest;
        var choice = Choosing(manifest);

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);

        Directory.CreateDirectory(directory);

        using (var writer = new StreamWriter(Path.Combine(directory, Examples)))
        {
            writer.WriteLine(string.Join(',', ExamplesHeader(manifest)));

            var decision = 0;

            foreach (var example in data.Games.SelectMany(g => g.Examples))
            {
                if (choice)
                    WriteChoice(writer, decision++, manifest.Features.Length, example);
                else
                    writer.WriteLine(string.Join(',', [.. example.Features.Select(Text), Text(example.Outcome)]));
            }
        }

        using (var writer = new StreamWriter(Path.Combine(directory, Games)))
        {
            writer.WriteLine(string.Join(',', GamesHeader));

            foreach (var game in data.Games)
                writer.WriteLine($"{game.GameId},{game.StartUtc.ToString("O", CultureInfo.InvariantCulture)},{Text(game.Examples.Length)}");
        }

        ContractFiles.WriteTrainingDataManifest(Path.Combine(directory, Manifest), manifest);
    }

    public static string[] GameIds(string directory) => [.. Rows(Path.Combine(directory, Games), GamesHeader).Select(r => r[0])];

    private static bool Choosing(TrainingDataManifest manifest) => manifest.Model.Distribution.ChoosesAmongCandidates();

    private static string[] ExamplesHeader(TrainingDataManifest manifest) =>
        Choosing(manifest) ? [Decision, .. manifest.Features, Outcome] : [.. manifest.Features, Outcome];

    private static void WriteChoice(StreamWriter writer, int decision, int features, TrainingExample example)
    {
        for (var row = 0; row < example.Features.Length / features; row++)
        {
            var values = example.Features.Skip(row * features).Take(features).Select(Text);

            writer.WriteLine(string.Join(',', [Text(decision), .. values, Text(row == example.Outcome ? 1 : 0)]));
        }
    }

    private static TrainingExample[] Choices(string directory, string[][] rows)
    {
        var decisions = rows.GroupBy(r => Whole(r[0])).ToArray();

        if (decisions.Select(d => d.Key).Where((decision, index) => decision != index).Any())
            throw new InvalidDataException($"{directory} does not number its decisions from 0 in order");

        return [.. decisions.Select(d => new TrainingExample([.. d.SelectMany(r => Numbers(r[1..^1]))], Chosen(directory, d.Key, [.. d.Select(r => Whole(r[^1]))])))];
    }

    private static int Chosen(string directory, int decision, int[] chosen)
    {
        if (chosen.Any(c => c is not (0 or 1)) || chosen.Sum() != 1)
            throw new InvalidDataException($"Decision {decision} in {directory} does not choose exactly one candidate");

        return Array.IndexOf(chosen, 1);
    }

    private static string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static double[] Numbers(string[] values) => [.. values.Select(v => double.Parse(v, CultureInfo.InvariantCulture))];

    private static int Whole(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static DateTime StartUtc(string value)
    {
        var moment = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return moment.Kind == DateTimeKind.Utc ? moment : throw new InvalidDataException($"{value} is not in UTC");
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
