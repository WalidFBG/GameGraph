using System.Globalization;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Simulation;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class LatestGameSimulationTests(ITestOutputHelper output)
{
    private const int Simulations = 10_000;

    [Fact]
    public void SimulatesTheLatestGeneratedGame()
    {
        var (game, context) = QualityAssuranceData.GeneratedGames().Last();
        var scores = new Scores(Simulations);

        Simulator.Run(
            QualityAssuranceData.FittedGame(),
            Simulations,
            simulationIndex => new NbaState(scores.For(simulationIndex), context.HomeScoringRate, context.AwayScoringRate));

        var simulations = Enumerable.Range(0, Simulations).ToArray();

        Assert.DoesNotContain(simulations, s => scores.Home(s) == scores.Away(s));

        var homeWins = simulations.Count(s => scores.Home(s) > scores.Away(s));
        var overtime = simulations.Count(s => scores.Periods[s] > Periods.Regulation);
        var path = Write(game, scores);

        output.WriteLine($"Game {game.Id}, {game.StartUtc:O}: {game.Possessions.Length} possessions in {game.Periods} periods");
        output.WriteLine($"Observed: home {game.Points(Sides.Home)}, away {game.Points(Sides.Away)}");
        output.WriteLine($"Simulated: home win {(double)homeWins / Simulations:P1}, away win {(double)(Simulations - homeWins) / Simulations:P1}, overtime {(double)overtime / Simulations:P1}, mean total {simulations.Average(s => scores.Home(s) + scores.Away(s)):F1}, mean possessions {simulations.Average(s => scores.Possessions[s]):F1}");
        output.WriteLine($"Written to {path}");
    }

    private static string Write(ObservedGame game, Scores scores)
    {
        var output = QualityAssuranceData.Output("latest-game-simulation");

        Directory.CreateDirectory(output);

        var path = Path.Combine(output, $"{game.Id}.csv");

        using var writer = new StreamWriter(path);

        writer.WriteLine("simulationIndex,home,away");

        for (var simulationIndex = 0; simulationIndex < Simulations; simulationIndex++)
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{simulationIndex},{scores.Home(simulationIndex)},{scores.Away(simulationIndex)}"));

        return path;
    }
}
