using System.Globalization;
using GameGraph.Contract;
using GameGraph.Contract.Files;
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
        var examples = Examples(data);
        var report = ContractFiles.ReadTrainingReport(Path.Combine(parity, "Golden.training.json"));
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(parity, "Golden.json")), data.Manifest.Features);
        var training = data.ExamplesIn(report["trainingGames"]!.GetValue<int>());

        Assert.Equal((training, examples.Length - training), (report["examples"]!["training"]!.GetValue<int>(), report["examples"]!["validation"]!.GetValue<int>()));
        var likelihood = report["attempts"]![report["chosen"]!.GetValue<int>()]!["negativeLogLikelihood"]!;

        AssertClose(likelihood["training"]!.GetValue<double>(), MeanNegativeLogLikelihood(model, examples[..training]));
        AssertClose(likelihood["validation"]!.GetValue<double>(), MeanNegativeLogLikelihood(model, examples[training..]));
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
        var examples = Examples(data);
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(parity, "Golden.json")), data.Manifest.Features);
        var lines = File.ReadAllLines(Path.Combine(parity, "samples.csv"));

        Assert.Equal("uniform,sample", lines[0]);
        Assert.Equal(examples.Length, lines.Length - 1);

        for (var e = 0; e < examples.Length; e++)
        {
            var values = lines[e + 1].Split(',');
            var uniform = double.Parse(values[0], CultureInfo.InvariantCulture);

            Assert.Equal(int.Parse(values[1], CultureInfo.InvariantCulture), model.Sample(examples[e].Features, uniform));
        }
    }

    private static string Parity(string set) => Path.Combine(Repository.Root, "testdata", set);

    private static TrainingExample[] Examples(TrainingData data) => [.. data.Games.SelectMany(g => g.Examples)];

    private static double MeanNegativeLogLikelihood(Model model, TrainingExample[] examples) =>
        -examples.Sum(e => model.LogProbability(e.Features, e.Outcome)) / examples.Length;

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected), $"Expected {expected:R}, got {actual:R}");
}
