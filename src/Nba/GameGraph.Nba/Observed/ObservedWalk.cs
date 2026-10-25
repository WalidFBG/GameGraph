using GameGraph.Engine;
using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public static class ObservedWalk
{
    public static ObservedOutcomeDecider Walk(ObservedGame game, NbaState state, IDecisionCollector<NbaState> decisions)
    {
        var decider = new ObservedOutcomeDecider(game.Possessions);

        GameWalker.Walk(NbaGameSpec.Instance, state, decider, decisions);
        decider.EnsureAllUsed();

        return decider;
    }
}
