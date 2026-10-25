using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaFeatures
{
    private const int LateSeconds = 120;

    public static readonly Feature<NbaState> ScoreMargin =
        new(nameof(ScoreMargin), s => s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> AtHome =
        new(nameof(AtHome), s => s.TeamInPossession == Sides.Home ? 1 : 0);

    public static readonly Feature<NbaState> ShooterAtHome =
        new(nameof(ShooterAtHome), s => s.AwardedTeam == Sides.Home ? 1 : 0);

    public static readonly Feature<NbaState> DefenceFreeThrows =
        new(nameof(DefenceFreeThrows), s => s.DefenceFreeThrows);

    public static readonly Feature<NbaState> TeamScoringRate =
        new(nameof(TeamScoringRate), s => s.ScoringRate[s.TeamInPossession]);

    public static readonly Feature<NbaState> DefenceTeamFouls =
        new(nameof(DefenceTeamFouls), s => s.TeamFouls[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> Period =
        new(nameof(Period), s => s.Period);

    public static readonly Feature<NbaState> ShotIsThree =
        new(nameof(ShotIsThree), s => s.ShotPoints == 3 ? 1 : 0);

    public static readonly Feature<NbaState> ShotFouled =
        new(nameof(ShotFouled), s => s.Fouled ? 1 : 0);

    public static readonly Feature<NbaState> LastFreeThrow =
        new(nameof(LastFreeThrow), s => s.FreeThrowsLeft == 1 ? 1 : 0);

    public static readonly Feature<NbaState> MissedThree =
        new(nameof(MissedThree), s => s.LastMiss == Miss.Three ? 1 : 0);

    public static readonly Feature<NbaState> MissedFreeThrow =
        new(nameof(MissedFreeThrow), s => s.LastMiss == Miss.FreeThrow ? 1 : 0);

    public static readonly Feature<NbaState> ShooterMargin =
        new(nameof(ShooterMargin), s => s.Score[s.AwardedTeam] - s.Score[Sides.Opponent(s.AwardedTeam)]);

    public static readonly Feature<NbaState> ShooterScoringRate =
        new(nameof(ShooterScoringRate), s => s.ScoringRate[s.AwardedTeam]);

    public static readonly Feature<NbaState> AfterMiss =
        new(nameof(AfterMiss), s => s.LastMiss != Miss.NotSet ? 1 : 0);

    public static readonly Feature<NbaState> ShotClock =
        new(nameof(ShotClock), s => s.ShotClock);

    public static readonly Feature<NbaState> PeriodClock =
        new(nameof(PeriodClock), s => s.PeriodClock);

    public static readonly Feature<NbaState> Bound =
        new(nameof(Bound), s => s.Bound);

    public static readonly Feature<NbaState> LogBound =
        new(nameof(LogBound), s => Math.Log(s.Bound + 1));

    public static readonly Feature<NbaState> PeriodClockBinds =
        new(nameof(PeriodClockBinds), s => s.PeriodClockBinds ? 1 : 0);

    public static readonly Feature<NbaState> LateInGame =
        new(nameof(LateInGame), s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds ? 1 : 0);

    public static readonly Feature<NbaState> ActionThree =
        new(nameof(ActionThree), s => s.Action == ActionType.ThreePointAttempt ? 1 : 0);

    public static readonly Feature<NbaState> ActionTurnover =
        new(nameof(ActionTurnover), s => s.Action == ActionType.Turnover ? 1 : 0);

    public static readonly Feature<NbaState> ActionFoul =
        new(nameof(ActionFoul), s => s.Action == ActionType.NonShootingFoul ? 1 : 0);

    public static readonly Feature<NbaState> AfterOffensiveRebound =
        new(nameof(AfterOffensiveRebound), s => s.Start == PlayStart.OffensiveRebound ? 1 : 0);

    public static readonly Feature<NbaState> AfterMadeShot =
        new(nameof(AfterMadeShot), s => s.Start == PlayStart.MadeShot ? 1 : 0);

    public static readonly Feature<NbaState> AfterMadeFreeThrow =
        new(nameof(AfterMadeFreeThrow), s => s.Start == PlayStart.MadeFreeThrow ? 1 : 0);

    public static readonly Feature<NbaState> AfterTurnover =
        new(nameof(AfterTurnover), s => s.Start == PlayStart.Turnover ? 1 : 0);

    public static readonly Feature<NbaState> AfterSideOut =
        new(nameof(AfterSideOut), s => s.Start == PlayStart.SideOut ? 1 : 0);

    public static readonly Feature<NbaState> AfterPeriodStart =
        new(nameof(AfterPeriodStart), s => s.Start == PlayStart.PeriodStart ? 1 : 0);
}
