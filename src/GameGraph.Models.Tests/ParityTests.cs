using System.Globalization;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class ParityTests
{
    private const double RelativeTolerance = 1e-12;

    private static readonly string Parity = Path.Combine(Repository.Root, "golden", "models", "bernoulli-training");

    [Fact]
    public void CSharpReproducesTheLikelihoodsPythonReported()
    {
        var data = TrainingDataFiles.Read(Parity);
        var report = ContractFiles.ReadTrainingReport(Path.Combine(Parity, "Golden.training.json"));
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(Parity, "Golden.json")), data.Manifest.Features);
        var training = data.ExamplesIn(report.TrainingGames);

        Assert.Equal(report.Examples, new TrainingAndValidation<int>(training, data.Outcomes.Length - training));
        AssertClose(report.NegativeLogLikelihood.Training, MeanNegativeLogLikelihood(model, data, 0, training));
        AssertClose(report.NegativeLogLikelihood.Validation, MeanNegativeLogLikelihood(model, data, training, data.Outcomes.Length));
    }

    [Fact]
    public void CSharpDrawsTheSamplesPythonDrew()
    {
        var data = TrainingDataFiles.Read(Parity);
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(Parity, "Golden.json")), data.Manifest.Features);
        var lines = File.ReadAllLines(Path.Combine(Parity, "samples.csv"));

        Assert.Equal("uniform,sample", lines[0]);
        Assert.Equal(data.Outcomes.Length, lines.Length - 1);

        for (var e = 0; e < data.Outcomes.Length; e++)
        {
            var values = lines[e + 1].Split(',');
            var uniform = double.Parse(values[0], CultureInfo.InvariantCulture);

            Assert.Equal(int.Parse(values[1], CultureInfo.InvariantCulture), model.Sample(data.Features[e], uniform));
        }
    }

    private static double MeanNegativeLogLikelihood(Model model, TrainingData data, int from, int to)
    {
        var total = 0.0;

        for (var e = from; e < to; e++)
            total -= model.LogProbability(data.Features[e], data.Outcomes[e]);

        return total / (to - from);
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected), $"Expected {expected:R}, got {actual:R}");
}
