using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaGameSpec
{
    public static GameSpec<NbaState> Instance { get; } = new(
        [
            NbaNodes.JumpBall,
            NbaNodes.OffenceTimeout,
            NbaNodes.DefenceTimeout,
            NbaNodes.AwardedFreeThrow,
            NbaNodes.AwardedFreeThrowMade,
            NbaNodes.PeriodRunsOut,
            NbaNodes.ActionType,
            NbaNodes.ActionTime,
            NbaNodes.Stolen,
            NbaNodes.Shooter,
            NbaNodes.FouledOnShot,
            NbaNodes.ShotMade,
            NbaNodes.InBonus,
            NbaNodes.FreeThrowMade,
            NbaNodes.OffensiveRebound,
            NbaNodes.TeamRebound,
        ],
        NbaNodes.JumpBall);
}
