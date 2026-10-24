using GameGraph.Engine;
using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedDecision(NodeSpec<NbaState> Node, Func<NbaState, int> Outcome, int TeamInPossession, int Period);
