using GameGraph.Nba.Graph;
using GameGraph.Nba.Fixture;
using GameGraph.Simulation;
using Xunit;

namespace GameGraph.Nba.Tests.Unit;

public sealed class SimulationDigestTests
{
    private const int Simulations = 1_000;

    private const string Digest = "1379d453587164f75ba99d59f9779150e4425d8ff9913cfde71d2b09d392e5f9";

    [Fact]
    public void TheFixedGamesSimulationsKeepTheirDigest()
    {
        var fitted = SimulationFixture.Fitted();
        var context = SimulationFixture.Context();
        var digest = new SimulationDigest();

        Simulator.Run(fitted, Seeds.Draw(Simulations), _ => new NbaState(digest, context), digest);

        Assert.Equal(Digest, digest.Hex());
    }
}
