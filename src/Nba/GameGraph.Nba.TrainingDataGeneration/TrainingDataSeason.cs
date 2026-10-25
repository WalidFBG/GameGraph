using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Replay;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class TrainingDataSeason(TrainingDataCollector<NbaState> collector, GameTally tally)
{
    private readonly EmptyIncidentCollector _incidentCollector = new();

    public void Walk(IReadOnlyCollection<ObservedGame> games)
    {
        foreach (var game in games.Where(g => !g.IsAccepted))
            tally.Rejected(game.Rejection);

        var generated = 0;

        foreach (var (game, context) in GeneratedGames.InOrder(games))
        {
            Generate(game, context);
            generated++;
        }

        tally.ShortHistories(games.Count(g => g.IsAccepted) - generated);
    }

    private void Generate(ObservedGame game, GameContext context)
    {
        collector.BeginGame(game.Id, game.StartUtc);

        var decider = ObservedWalk.Walk(game, new NbaState(_incidentCollector, context.HomePointsPerPossession, context.AwayPointsPerPossession), collector);

        tally.Generated();
        tally.ActionTimes(decider.ActionTimes, decider.ClampedActionTimes);
    }
}
