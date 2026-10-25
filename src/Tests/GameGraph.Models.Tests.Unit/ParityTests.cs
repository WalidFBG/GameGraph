using System.Globalization;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests.Unit;

public sealed class ParityTests
{
    private const double RelativeTolerance = 1e-12;

    public static TheoryData<string> Sets => new("bernoulli-training", "categorical-training", "johnson-su-training", "choice-training");

    [Theory]
    [MemberData(nameof(Sets))]
    public void CSharpReproducesTheLikelihoodsPythonReported(string set)
    {
        var parity = Parity(set);
        var data = TrainingDataFiles.Read(parity);
        var report = ContractFiles.ReadTrainingReport(Path.Combine(parity, "Golden.training.json"));
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(parity, "Golden.json")), data.Manifest.Features);
        var training = data.ExamplesIn(report.TrainingGames);

        Assert.Equal(report.Examples, new TrainingAndValidation<int>(training, data.Outcomes.Length - training));
        var likelihood = report.ChosenAttempt().NegativeLogLikelihood;

        AssertClose(likelihood.Training, MeanNegativeLogLikelihood(model, data, 0, training));
        AssertClose(likelihood.Validation, MeanNegativeLogLikelihood(model, data, training, data.Outcomes.Length));
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void TheFittedModelCarriesItsManifestsDeclaration(string set)
    {
        var parity = Parity(set);
        var manifest = ContractFiles.ReadTrainingDataManifest(Path.Combine(parity, "manifest.json"));
        var fitted = ContractFiles.ReadFittedModel(Path.Combine(parity, "Golden.json"));

        Assert.Equal(manifest.Model, fitted.Model);
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void CSharpDrawsTheSamplesPythonDrew(string set)
    {
        var parity = Parity(set);
        var data = TrainingDataFiles.Read(parity);
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(parity, "Golden.json")), data.Manifest.Features);
        var lines = File.ReadAllLines(Path.Combine(parity, "samples.csv"));

        Assert.Equal("uniform,sample", lines[0]);
        Assert.Equal(data.Outcomes.Length, lines.Length - 1);

        for (var e = 0; e < data.Outcomes.Length; e++)
        {
            var values = lines[e + 1].Split(',');
            var uniform = double.Parse(values[0], CultureInfo.InvariantCulture);

            Assert.Equal(int.Parse(values[1], CultureInfo.InvariantCulture), model.Sample(data.Features[e], uniform));
        }
    }

    private static string Parity(string set) => Path.Combine(Repository.Root, "testdata", set);

    private static double MeanNegativeLogLikelihood(Model model, TrainingDataFile data, int from, int to)
    {
        var total = 0.0;

        for (var e = from; e < to; e++)
            total -= model.LogProbability(data.Features[e], data.Outcomes[e]);

        return total / (to - from);
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected), $"Expected {expected:R}, got {actual:R}");
}
