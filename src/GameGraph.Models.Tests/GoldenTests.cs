using System.Text.Json;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class GoldenTests
{
    private const double RelativeTolerance = 1e-12;

    public static TheoryData<string> Distributions => new("bernoulli", "categorical", "johnson-su");

    [Theory]
    [MemberData(nameof(Distributions))]
    public void CSharpAgreesWithPython(string distribution)
    {
        var directory = Path.Combine(Repository.Root, "golden", "models", distribution);
        var golden = JsonSerializer.Deserialize<GoldenCases>(File.ReadAllText(Path.Combine(directory, "cases.json")), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("No golden cases");
        var model = Model.FromFitted(ContractFiles.ReadFittedModel(Path.Combine(directory, "fitted-model.json")), golden.Features);

        Assert.NotEmpty(golden.Cases);

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

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected), $"Expected {expected:R}, got {actual:R}");

    private sealed record GoldenCases(string[] Features, GoldenCase[] Cases);

    private sealed record GoldenCase(double[] Features, int Outcome, double[] Parameters, double LogProbability, double Uniform, int Sample);
}
