using GameGraph.Engine;
using GameGraph.Nba.Graph;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed record ObservedDecision(NodeSpec<NbaState> Node, int Outcome, int TeamInPossession, int Period);
