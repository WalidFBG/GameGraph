using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Replay;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class ObservedTotals
{
    public static GameTotals Of(ObservedGame game, GameContext context)
    {
        var score = new GameScore();

        ObservedWalk.Walk(game, new NbaState(score, context, game.Rotation), new EmptyDecisionCollector<NbaState>());

        var totals = score.Totals();

        if (totals.HomePoints != game.Points(Sides.Home) || totals.AwayPoints != game.Points(Sides.Away))
            throw new InvalidDataException($"Game {game.Id}: walking it scores {totals.HomePoints}-{totals.AwayPoints}, the data {game.Points(Sides.Home)}-{game.Points(Sides.Away)}");

        return totals;
    }
}
