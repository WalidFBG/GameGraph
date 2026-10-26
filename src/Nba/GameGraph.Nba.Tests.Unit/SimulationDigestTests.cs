using GameGraph.Nba.Fixture;
using GameGraph.Nba.Graph;
using GameGraph.Simulation.Randomness;
using GameGraph.Simulation;
using Xunit;

namespace GameGraph.Nba.Tests.Unit;

public sealed class SimulationDigestTests
{
    private const int Simulations = 1_000;

    private const string Digest = "15693f2e60d2efc7ac0830b4b9144b3373c331432779c5ce6272fd97c8fb7fdd";

    [Fact]
    public void TheFixedGamesSimulationsAskEveryNodeAndKeepTheirDigest()
    {
        var fitted = SimulationFixture.Fitted();
        var context = SimulationFixture.Context();
        var digest = new SimulationDigest();

        Simulator.Run(fitted, Seeds.Draw(Simulations), _ => new NbaState(digest, context), digest);

        Assert.Empty(NbaGraph.Instance.Nodes.Where(n => digest.Decisions(n) == 0).Select(n => n.Name));
        Assert.Equal(Digest, digest.Hex());
    }
}
