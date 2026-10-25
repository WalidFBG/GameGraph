using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class SeasonSimulation
{
    public static (TrainingDataCollector<NbaState> Decisions, GameComparison[] Games) SimulateEachOnce(
        FittedGame<NbaState> fitted,
        IReadOnlyList<(AcceptedGame Game, GameContext Context)> games)
    {
        var seeds = Seeds.Draw(games.Count);
        var decisions = new TrainingDataCollector<NbaState>(NbaGameSpec.Instance);
        var comparisons = new GameComparison[games.Count];

        for (var g = 0; g < games.Count; g++)
        {
            var (game, context) = games[g];
            var score = new GameScore();

            decisions.BeginGame(game.Id, game.StartUtc);
            Simulator.Run(fitted, [seeds[g]], _ => new NbaState(score, context, game.Rotation), decisions);

            comparisons[g] = new GameComparison(game.Id, ObservedTotals.Of(game, context), score.Totals());
        }

        return (decisions, comparisons);
    }
}
