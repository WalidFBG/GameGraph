using GameGraph.Nba.Audit.Definitions;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Simulation;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Nba.Audit.Domain;

internal static class SeasonSimulation
{
    public static (TrainingDataCollector<NbaState> Decisions, GameComparison[] Games) SimulateEachOnce(
        FittedGame<NbaState> fitted,
        IReadOnlyList<(ObservedGame Game, GameContext Context)> games)
    {
        var seeds = Seeds.Draw(games.Count);
        var decisions = new TrainingDataCollector<NbaState>(NbaGameSpec.Instance);
        var comparisons = new GameComparison[games.Count];

        for (var g = 0; g < games.Count; g++)
        {
            var (game, context) = games[g];
            var score = new GameScore();

            decisions.BeginGame(game.Id, game.StartUtc);
            Simulator.Run(fitted, [seeds[g]], _ => new NbaState(score, context.HomeScoringRate, context.AwayScoringRate), decisions);

            comparisons[g] = new GameComparison(game.Id, RealTotals(game), score.Totals());
        }

        return (decisions, comparisons);
    }

    private static GameTotals RealTotals(ObservedGame game) =>
        new(game.Points(Sides.Home), game.Points(Sides.Away), game.Possessions.Length, game.Periods);
}
