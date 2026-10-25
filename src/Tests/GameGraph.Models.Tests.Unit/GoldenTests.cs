using System.Text.Json;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests.Unit;

public sealed class GoldenTests
{
    private const double RelativeTolerance = 1e-12;

    public static TheoryData<string> Distributions => new("bernoulli", "categorical", "johnson-su", "johnson-su-narrow", "choice");

    [Theory]
    [MemberData(nameof(Distributions))]
    public void CSharpAgreesWithPython(string distribution)
    {
        var (golden, model) = Read(distribution);

        foreach (var c in golden.Cases)
        {
            var parameters = new double[c.Parameters.Length];
            model.ParameterValues(c.Features, parameters);

            for (var p = 0; p < parameters.Length; p++)
                AssertClose(c.Parameters[p], parameters[p]);

            AssertClose(c.LogProbability, model.LogProbability(c.Features, c.Outcome));
            Assert.Equal(c.Sample, model.Sample(c.Features, c.Uniform));
        }
    }

    [Theory]
    [MemberData(nameof(Distributions))]
    public void ProbabilitiesAreEveryOutcomesAndSumToOne(string distribution)
    {
        var (golden, model) = Read(distribution);

        foreach (var c in golden.Cases)
        {
            var probabilities = model.Probabilities(c.Features);

            Assert.InRange(c.Outcome, 0, probabilities.Length - 1);
            AssertClose(Math.Exp(c.LogProbability), probabilities[c.Outcome]);
            Assert.Equal(1, probabilities.Sum(), 1e-12);
        }
    }

    private static (GoldenCases Golden, Model Model) Read(string distribution)
    {
        var directory = Path.Combine(Repository.Root, "testdata", distribution);
        var golden = JsonSerializer.Deserialize<GoldenCases>(File.ReadAllText(Path.Combine(directory, "cases.json")), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("No golden cases");

        Assert.NotEmpty(golden.Cases);

        return (golden, Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(directory, "fitted-model.json")), golden.Features));
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected), $"Expected {expected:R}, got {actual:R}");

    private sealed record GoldenCases(string[] Features, GoldenCase[] Cases);

    private sealed record GoldenCase(double[] Features, int Outcome, double[] Parameters, double LogProbability, double Uniform, int Sample);
}
