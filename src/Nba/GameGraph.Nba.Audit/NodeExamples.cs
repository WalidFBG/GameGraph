using System.Globalization;
using GameGraph.Models.Contract;

namespace GameGraph.Nba.Audit;

internal sealed record NodeExamples(TrainingDataManifest Manifest, double[][] Features, int[] Outcomes)
{
    public static NodeExamples Read(string directory)
    {
        var manifest = ContractFiles.ReadTrainingDataManifest(Path.Combine(directory, "manifest.json"));
        var path = Path.Combine(directory, "examples.csv");
        var lines = File.ReadAllLines(path);
        var header = string.Join(',', [.. manifest.Features, "outcome"]);

        if (lines.Length == 0 || lines[0] != header)
            throw new InvalidDataException($"The header of {path} is not {header}");

        if (lines.Length - 1 != manifest.Examples)
            throw new InvalidDataException($"{path} has {lines.Length - 1} examples, the manifest says {manifest.Examples}");

        var features = new double[manifest.Examples][];
        var outcomes = new int[manifest.Examples];

        for (var e = 0; e < manifest.Examples; e++)
        {
            var values = lines[e + 1].Split(',');

            if (values.Length != manifest.Features.Length + 1)
                throw new InvalidDataException($"Example {e} of {path} does not have {manifest.Features.Length + 1} columns");

            features[e] = [.. values[..^1].Select(v => double.Parse(v, CultureInfo.InvariantCulture))];
            outcomes[e] = int.Parse(values[^1], CultureInfo.InvariantCulture);
        }

        return new NodeExamples(manifest, features, outcomes);
    }
}
