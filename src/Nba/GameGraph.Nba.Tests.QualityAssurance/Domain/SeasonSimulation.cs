using GameGraph.Engine.Walk;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Simulation.Randomness;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class SeasonSimulation
{
    public static (ExampleRecorder<NbaState> Examples, GameComparison[] Games) SimulateEachOnce(
        FittedGraph<NbaState> fitted,
        IReadOnlyList<(AcceptedGame Game, GameContext Context)> games)
    {
        var seeds = Seeds.Draw(games.Count);
        var examples = new ExampleRecorder<NbaState>(NbaGraph.Instance);
        var comparisons = new GameComparison[games.Count];

        for (var g = 0; g < games.Count; g++)
        {
            var (game, context) = games[g];
            var score = new GameScore();

            Simulator.Run(fitted, [seeds[g]], _ => new NbaState(score, context), examples.BeginGame(game.Id, game.StartUtc));

            comparisons[g] = new GameComparison(game.Id, ObservedTotals.Of(game, context), score.Totals());
        }

        return (examples, comparisons);
    }
}
