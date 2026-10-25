using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Tests.QualityAssurance.Domain;
using GameGraph.Nba.Tests.QualityAssurance.Infrastructure;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class LatestGameSimulationTests(ITestOutputHelper output)
{
    private const int SimulationCount = 10_000;

    [Fact]
    public void SimulatesTheLatestGeneratedGame()
    {
        var (game, context) = GeneratedGames.InOrder(ValidatedDataReader.ReadAll()).Last();
        var simulations = Simulations.Of(FittedModelReader.ReadGame(), game, context, SimulationCount);

        Assert.DoesNotContain(simulations, s => s.HomePoints == s.AwayPoints);

        var homeWins = simulations.Count(s => s.HomePoints > s.AwayPoints);
        var overtime = simulations.Count(s => s.Periods > Periods.Regulation);
        var path = SimulatedScoresWriter.Write(game.Id, simulations);

        output.WriteLine($"Game {game.Id}, {game.StartUtc:O}: {game.Possessions.Length} possessions in {game.Periods} periods");
        output.WriteLine($"Observed: home {game.Points(Sides.Home)}, away {game.Points(Sides.Away)}");
        output.WriteLine($"Simulated: home win {(double)homeWins / SimulationCount:P1}, away win {(double)(SimulationCount - homeWins) / SimulationCount:P1}, overtime {(double)overtime / SimulationCount:P1}, mean total {simulations.Average(s => s.HomePoints + s.AwayPoints):F1}, mean possessions {simulations.Average(s => s.Possessions):F1}");
        output.WriteLine($"Written to {path}");
    }
}
