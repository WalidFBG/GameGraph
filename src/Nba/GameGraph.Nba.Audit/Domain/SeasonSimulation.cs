using GameGraph.Engine;
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

            comparisons[g] = new GameComparison(game.Id, RealTotals(game, context), score.Totals());
        }

        return (decisions, comparisons);
    }

    private static GameTotals RealTotals(ObservedGame game, GameContext context)
    {
        var score = new GameScore();

        ObservedWalk.Walk(game, new NbaState(score, context.HomeScoringRate, context.AwayScoringRate), new EmptyDecisionCollector<NbaState>());

        var totals = score.Totals();

        if (totals.HomePoints != game.Points(Sides.Home) || totals.AwayPoints != game.Points(Sides.Away))
            throw new InvalidDataException($"Game {game.Id}: walking it scores {totals.HomePoints}-{totals.AwayPoints}, the data {game.Points(Sides.Home)}-{game.Points(Sides.Away)}");

        return totals;
    }
}
