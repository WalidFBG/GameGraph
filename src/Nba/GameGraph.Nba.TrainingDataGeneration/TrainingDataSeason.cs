using GameGraph.Engine;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class TrainingDataSeason(TrainingDataCollector<NbaState> collector, GameTally tally)
{
    private readonly EmptyIncidentCollector _incidentCollector = new();

    public void Walk(IEnumerable<ObservedGame> games, TeamHistory history)
    {
        foreach (var game in games.OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (!game.IsAccepted)
            {
                tally.Rejected(game.Rejection);
                continue;
            }

            if (history.IsComplete(game))
            {
                Generate(game, history.ContextFor(game));
                tally.Generated();
            }
            else
            {
                tally.ShortHistory();
            }

            history.Advance(game);
        }
    }

    private void Generate(ObservedGame game, GameContext context)
    {
        var state = new NbaState(_incidentCollector, game.Schedule(), context.HomeScoringRate, context.AwayScoringRate);
        var decider = new ObservedOutcomeDecider(game.Possessions);

        collector.BeginGame(game.Id, game.StartUtc);
        GameWalker.Walk(NbaGameSpec.Instance, state, decider, collector);

        decider.EnsureAllUsed();
    }
}
