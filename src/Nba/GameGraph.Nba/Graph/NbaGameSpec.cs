using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaGameSpec
{
    public static GameSpec<NbaState, INbaIncidentCollector> Instance { get; } = new(
        [NbaNodes.ActionType, NbaNodes.FouledOnShot, NbaNodes.ShotMade, NbaNodes.InBonus, NbaNodes.FreeThrowMade, NbaNodes.OffensiveRebound],
        NbaNodes.ActionType);
}
