using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Replay;

public static class ObservedWalk
{
    public static ObservedOutcomeDecider Walk(AcceptedGame game, NbaState state, IDecisionCollector<NbaState> decisions)
    {
        var decider = new ObservedOutcomeDecider(game);

        GameWalker.Walk(NbaGameSpec.Instance, state, decider, decisions);
        decider.EnsureAllUsed();

        return decider;
    }
}
