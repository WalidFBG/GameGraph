using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaGameSpec
{
    public static GameSpec<NbaState> Instance { get; } = new(
        [
            NbaNodes.JumpBall,
            NbaNodes.AwardedFreeThrow,
            NbaNodes.AwardedFreeThrowMade,
            NbaNodes.AwardedBall,
            NbaNodes.PeriodRunsOut,
            NbaNodes.ActionType,
            NbaNodes.ActionTime,
            NbaNodes.FouledOnShot,
            NbaNodes.ShotMade,
            NbaNodes.InBonus,
            NbaNodes.FreeThrowMade,
            NbaNodes.OffensiveRebound,
        ],
        NbaNodes.JumpBall);
}
