using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaGameSpec
{
    public static GameSpec<NbaState> Instance { get; } = new(
        [
            NbaNodes.Starters,
            NbaNodes.JumpBall,
            NbaNodes.OffenceTimeout,
            NbaNodes.DefenceTimeout,
            NbaNodes.Substitutions,
            NbaNodes.SubstitutionCount,
            NbaNodes.SubstitutedOff,
            NbaNodes.SubstitutedOn,
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
        NbaNodes.Starters);
}
