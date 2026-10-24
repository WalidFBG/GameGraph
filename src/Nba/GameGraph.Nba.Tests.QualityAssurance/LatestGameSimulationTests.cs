using System.Globalization;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Sportradar;
using GameGraph.Models.Contract;
using GameGraph.Simulation;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class LatestGameSimulationTests(ITestOutputHelper output)
{
    private const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";
    private const string Root = @"C:\Data\GameGraph\nba";
    private const string FittedModels = Root + @"\fitted-models";
    private const string Output = Root + @"\quality-assurance\latest-game-simulation";
    private const int Simulations = 10_000;

    [Fact]
    public void SimulatesTheLatestGeneratedGame()
    {
        var (game, context) = LatestGeneratedGame();
        var fitted = NbaGameSpec.Instance.Nodes.Select(n => ContractFiles.ReadFittedModel(Path.Combine(FittedModels, $"{n.Name}.json"))).ToArray();
        var scores = new Scores(Simulations);

        Simulator.Run(
            new FittedGame<NbaState>(NbaGameSpec.Instance, fitted),
            Simulations,
            simulationIndex => new NbaState(scores.For(simulationIndex), context.HomeScoringRate, context.AwayScoringRate));

        var simulations = Enumerable.Range(0, Simulations).ToArray();

        Assert.DoesNotContain(simulations, s => scores.Home(s) == scores.Away(s));

        var homeWins = simulations.Count(s => scores.Home(s) > scores.Away(s));
        var overtime = simulations.Count(s => scores.Periods[s] > Periods.Regulation);
        var path = Write(game, scores);

        output.WriteLine($"Game {game.Id}, {game.StartUtc:O}: {game.Possessions.Length} possessions in {game.Possessions.Max(p => p.Period) + 1} periods");
        output.WriteLine($"Observed: home {Observed(game, Sides.Home)}, away {Observed(game, Sides.Away)}");
        output.WriteLine($"Simulated: home win {(double)homeWins / Simulations:P1}, away win {(double)(Simulations - homeWins) / Simulations:P1}, overtime {(double)overtime / Simulations:P1}, mean total {simulations.Average(s => scores.Home(s) + scores.Away(s)):F1}, mean possessions {simulations.Average(s => scores.Possessions[s]):F1}");
        output.WriteLine($"Written to {path}");
    }

    private static (ObservedGame Game, GameContext Context) LatestGeneratedGame()
    {
        var history = new TeamHistory();
        (ObservedGame Game, GameContext Context)? latest = null;

        foreach (var game in new ValidatedGameReader(ValidatedData).ReadAll().OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (!game.IsAccepted)
                continue;

            if (history.IsComplete(game))
                latest = (game, history.ContextFor(game));

            history.Advance(game);
        }

        return latest ?? throw new InvalidDataException("No game has a complete history");
    }

    private static int Observed(ObservedGame game, int side) =>
        game.Possessions.Where(p => p.TeamInPossession == side).Sum(p => p.Points)
        + game.Possessions.SelectMany(p => p.Actions).SelectMany(a => a.AwardedBefore).Count(f => f.Team == side && f.Made);

    private static string Write(ObservedGame game, Scores scores)
    {
        Directory.CreateDirectory(Output);

        var path = Path.Combine(Output, $"{game.Id}.csv");

        using var writer = new StreamWriter(path);

        writer.WriteLine("simulationIndex,home,away");

        for (var simulationIndex = 0; simulationIndex < Simulations; simulationIndex++)
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{simulationIndex},{scores.Home(simulationIndex)},{scores.Away(simulationIndex)}"));

        return path;
    }
}
