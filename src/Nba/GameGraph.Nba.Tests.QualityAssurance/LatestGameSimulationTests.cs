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
        var schedule = game.Schedule();
        var fitted = NbaGameSpec.Instance.Nodes.Select(n => ContractFiles.ReadFittedModel(Path.Combine(FittedModels, $"{n.Name}.json"))).ToArray();
        var scores = new Scores(Simulations);

        Simulator.Run(
            new FittedGame<NbaState>(NbaGameSpec.Instance, fitted),
            Simulations,
            simulationIndex => new NbaState(scores.For(simulationIndex), schedule, context.HomeScoringRate, context.AwayScoringRate));

        var homeWins = 0;
        var awayWins = 0;
        var ties = 0;

        for (var simulationIndex = 0; simulationIndex < Simulations; simulationIndex++)
        {
            Assert.Equal(game.Possessions.Length, scores.Possessions[simulationIndex]);

            if (scores.Home(simulationIndex) > scores.Away(simulationIndex))
                homeWins++;
            else if (scores.Home(simulationIndex) < scores.Away(simulationIndex))
                awayWins++;
            else
                ties++;
        }

        Assert.Equal(Simulations, homeWins + awayWins + ties);

        var path = Write(game, scores);
        var meanTotal = Enumerable.Range(0, Simulations).Average(s => scores.Home(s) + scores.Away(s));

        output.WriteLine($"Game {game.Id}, {game.StartUtc:O}: {game.Possessions.Length} possessions in {schedule.Length} periods");
        output.WriteLine($"Observed: home {Observed(game, Sides.Home)}, away {Observed(game, Sides.Away)}");
        output.WriteLine($"Simulated: home win {(double)homeWins / Simulations:P1}, away win {(double)awayWins / Simulations:P1}, tie {(double)ties / Simulations:P1}, mean total {meanTotal:F1}");
        output.WriteLine($"Written to {path}");
    }

    private static (ObservedGame Game, GameContext Context) LatestGeneratedGame()
    {
        var history = new TeamHistory();
        (ObservedGame Game, GameContext Context)? latest = null;

        foreach (var game in new ValidatedGameReader(ValidatedData).ReadAll().OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal).Select(g => g.WithoutCutPossessions()))
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
        game.Possessions.Where(p => p.TeamInPossession == side).Sum(p => p.Points);

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
