using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed record ObservedDecision(NodeSpec<NbaState, INbaIncidentCollector> Node, int Outcome, int TeamInPossession, int Period);
