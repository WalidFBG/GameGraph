using System.Text.Json;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class GoldenTests
{
    private const double RelativeTolerance = 1e-12;
    private const double AbsoluteTolerance = 1e-15;

    private static readonly string Bernoulli = Path.Combine(Repository.Root, "golden", "models", "bernoulli");

    [Fact]
    public void BernoulliAgreesWithPython()
    {
        var golden = JsonSerializer.Deserialize<GoldenCases>(File.ReadAllText(Path.Combine(Bernoulli, "cases.json")), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("No golden cases");
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(Bernoulli, "fitted-model.json")), golden.Features);

        Assert.NotEmpty(golden.Cases);

        foreach (var c in golden.Cases)
        {
            AssertClose(c.Probability, model.ParameterValues(c.Features)[0]);
            AssertClose(c.LogProbability, model.LogProbability(c.Features, c.Outcome));
            Assert.Equal(c.Sample, model.Sample(c.Features, c.Uniform));
        }
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected) + AbsoluteTolerance, $"Expected {expected:R}, got {actual:R}");

    private sealed record GoldenCases(string[] Features, GoldenCase[] Cases);

    private sealed record GoldenCase(double[] Features, int Outcome, double Probability, double LogProbability, double Uniform, int Sample);
}
