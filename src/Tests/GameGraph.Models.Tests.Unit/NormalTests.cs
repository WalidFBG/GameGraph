using System.Text.Json;
using Xunit;

namespace GameGraph.Models.Tests.Unit;

public sealed class NormalTests
{
    private const double RelativeTolerance = 1e-13;
    private const double SmallestCompared = 1e-300;

    private static readonly NormalCases Golden = JsonSerializer.Deserialize<NormalCases>(
            File.ReadAllText(Path.Combine(Repository.Root, "testdata", "normal", "cases.json")),
            JsonSerializerOptions.Web)
        ?? throw new InvalidDataException("No golden normal cases");

    [Fact]
    public void TheDistributionFunctionAgreesWithTorch()
    {
        foreach (var c in Golden.Cdf)
        {
            AssertClose(c.Cdf, Normal.Cdf(c.Z), $"cdf({c.Z})", 1e-12);
            AssertClose(c.LogCdf, Normal.LogCdf(c.Z), $"log cdf({c.Z})");
        }
    }

    [Fact]
    public void TheInverseDistributionFunctionAgreesWithTorch()
    {
        foreach (var c in Golden.InverseCdf)
            AssertClose(c.Z, Normal.InverseCdf(c.P), $"inverse cdf({c.P})");
    }

    private static void AssertClose(double expected, double actual, string what, double tolerance = RelativeTolerance) =>
        Assert.True(Math.Abs(actual - expected) <= tolerance * Math.Abs(expected) + SmallestCompared, $"{what}: expected {expected:R}, got {actual:R}");

    private sealed record NormalCases(CdfCase[] Cdf, InverseCdfCase[] InverseCdf);

    private sealed record CdfCase(double Z, double Cdf, double LogCdf);

    private sealed record InverseCdfCase(double P, double Z);
}
