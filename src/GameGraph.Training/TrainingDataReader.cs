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

        return new TrainingData(node, features, examples.ToArray(), outcomes.ToArray());
    }
}
