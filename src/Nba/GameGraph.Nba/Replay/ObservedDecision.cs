using GameGraph.Engine;
using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Replay;

public sealed record ObservedDecision(NodeSpec<NbaState> Node, Func<NbaState, int> Outcome, int TeamInPossession, int Period);
