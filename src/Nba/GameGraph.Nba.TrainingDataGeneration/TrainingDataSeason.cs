using GameGraph.Engine;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class TrainingDataSeason(IWalkSink<NbaState> sink, GameTally tally)
{
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
        var state = new NbaState(Schedule(game.Possessions), context.HomeScoringRate, context.AwayScoringRate);
        var decider = new ObservedOutcomeDecider(game.Possessions);

        GameWalker.Walk(NbaGameSpec.Instance, state, decider, sink);

        decider.EnsureAllUsed();
    }

    private static PeriodSchedule[] Schedule(Possession[] possessions) =>
        possessions
            .GroupBy(p => p.Period)
            .OrderBy(g => g.Key)
            .Select(g => new PeriodSchedule(g.First().Side, g.Count()))
            .ToArray();
}
