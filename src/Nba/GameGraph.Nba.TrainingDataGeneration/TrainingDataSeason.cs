using GameGraph.Engine;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Replay;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class TrainingDataSeason(RecordingDecisionCollector<NbaState> decisions, GameTally tally)
{
    private readonly EmptyIncidentCollector _incidentCollector = new();

    public void Walk(IReadOnlyCollection<ObservedGame> games)
    {
        foreach (var game in games.OfType<RejectedGame>())
            tally.Rejected(game.Reason);

        var generated = 0;

        foreach (var (game, context) in GeneratedGames.InOrder(games))
        {
            Generate(game, context);
            generated++;
        }

        tally.ShortHistories(games.OfType<AcceptedGame>().Count() - generated);
    }

    private void Generate(AcceptedGame game, GameContext context)
    {
        decisions.BeginGame(game.Id, game.StartUtc);

        var decider = ObservedWalk.Walk(game, new NbaState(_incidentCollector, context), decisions);

        tally.Generated();
        tally.ActionTimes(decider.ActionTimes, decider.ClampedActionTimes);
    }
}
