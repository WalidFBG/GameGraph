using System.Text.Json;

namespace GameGraph.Training;

public sealed class ParametersWriter(string root)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Write(TrainingData data, Fit fit, string source)
    {
        var path = Path.Combine(root, $"{data.Node}.json");

        var parameters = new
        {
            data.Node,
            Source = source,
            CreatedUtc = DateTime.UtcNow,
            Intercept = fit.Parameters[0],
            Weights = data.Features.Select((f, i) => (f, fit.Parameters[i + 1])).ToDictionary(),
            NegativeLogLikelihood = fit.NegativeLogLikelihoods[^1],
        };

        Directory.CreateDirectory(root);
        File.WriteAllText(path, JsonSerializer.Serialize(parameters, Json));

        return path;
    }
}
