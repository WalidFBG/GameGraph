using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaGameSpec
{
    public static GameSpec<NbaState> Instance { get; } = new(
        [NbaNodes.ActionType, NbaNodes.FouledOnShot, NbaNodes.ShotMade, NbaNodes.InBonus, NbaNodes.FreeThrowMade, NbaNodes.OffensiveRebound],
        NbaNodes.ActionType);
}
