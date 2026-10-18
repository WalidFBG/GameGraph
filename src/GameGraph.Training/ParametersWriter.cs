using System.Text.Json;
using GameGraph.ModelFamilies;
using GameGraph.Parameters;

namespace GameGraph.Training;

public sealed class ParametersWriter(string root)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Write(TrainingData data, Model model, Fit fit, string source)
    {
        var parameters = new NodeParameters(data.Node, model.Family.Name, model.Describe(fit.Parameters));

        var report = new
        {
            data.Node,
            Source = source,
            CreatedUtc = DateTime.UtcNow,
            fit.BestStep,
            fit.LastStep,
            TrainingNegativeLogLikelihood = fit.TrainingNegativeLogLikelihoods[fit.BestStep],
            ValidationNegativeLogLikelihood = fit.ValidationNegativeLogLikelihoods[fit.BestStep],
        };

        var path = ParametersFile.Write(root, parameters);
        File.WriteAllText(Path.Combine(root, $"{data.Node}.training.json"), JsonSerializer.Serialize(report, Json));

        return path;
    }
}
