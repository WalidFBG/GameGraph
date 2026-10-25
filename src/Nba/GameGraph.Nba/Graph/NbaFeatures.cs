using System.Linq.Expressions;
using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaFeatures
{
    public const int LateSeconds = 120;

    public static readonly Feature<NbaState> ScoreMargin =
        new(nameof(ScoreMargin), s => s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> AtHome =
        new(nameof(AtHome), s => s.TeamInPossession == Sides.Home ? 1 : 0);

    public static readonly Feature<NbaState> ShooterAtHome =
        new(nameof(ShooterAtHome), s => s.AwardedTeam == Sides.Home ? 1 : 0);

    public static readonly Feature<NbaState> TeamPointsPerPossession =
        new(nameof(TeamPointsPerPossession), s => s.PointsPerPossession[s.TeamInPossession]);

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

    public static readonly Feature<NbaState> ShooterPointsPerPossession =
        new(nameof(ShooterPointsPerPossession), s => s.PointsPerPossession[s.AwardedTeam]);

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

    public static readonly Feature<NbaState> LateMargin =
        new(nameof(LateMargin), s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds ? s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)] : 0);

    public static readonly Feature<NbaState> LatePeriodClock =
        new(nameof(LatePeriodClock), s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds ? s.PeriodClock : 0);

    public static readonly Feature<NbaState> LateFoul =
        new(nameof(LateFoul), s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds && s.Action == ActionType.NonShootingFoul ? 1 : 0);

    public static readonly Feature<NbaState> AfterTimeout =
        new(nameof(AfterTimeout), s => s.AfterTimeout ? 1 : 0);

    public static readonly Feature<NbaState> LateAfterTimeout =
        new(nameof(LateAfterTimeout), s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds && s.AfterTimeout ? 1 : 0);

    public static readonly Feature<NbaState> BeforeFreeThrow =
        new(nameof(BeforeFreeThrow), s => s.TimeoutMoment == TimeoutMoment.FreeThrow ? 1 : 0);

    public static readonly Feature<NbaState> TimeoutJustCalled =
        new(nameof(TimeoutJustCalled), s => s.TimeoutJustCalled ? 1 : 0);

    public static readonly IReadOnlyList<(Feature<NbaState> Inside, Feature<NbaState> Clock)> PeriodTimeouts =
        [PeriodTimeoutsSoFar("None", 0, 0), PeriodTimeoutsSoFar("One", 1, 1), PeriodTimeoutsSoFar("TwoOrMore", 2, int.MaxValue)];

    public static (Feature<NbaState> Inside, Feature<NbaState> Clock) LateBand(string name, int lowestMargin, int highestMargin) =>
    (
        new($"Late{name}", s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds && s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)] >= lowestMargin && s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)] <= highestMargin ? 1 : 0),
        new($"Late{name}Clock", s => s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds && s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)] >= lowestMargin && s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)] <= highestMargin ? s.PeriodClock : 0)
    );

    public static readonly Feature<NbaState> OffenceTimeoutsLeft =
        new(nameof(OffenceTimeoutsLeft), s => s.TimeoutsLeft[s.TeamInPossession]);

    public static readonly Feature<NbaState> DefenceTimeoutsLeft =
        new(nameof(DefenceTimeoutsLeft), s => s.TimeoutsLeft[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> DefenceInPenalty =
        new(nameof(DefenceInPenalty), s => s.TeamFouls[Sides.Opponent(s.TeamInPossession)] >= (s.Period < Periods.Regulation ? FreeThrows.PenaltyFoul : FreeThrows.OvertimePenaltyFoul) ? 1 : 0);

    public static readonly Feature<NbaState> DefenceSecondFoulInLastTwoMinutes =
        new(nameof(DefenceSecondFoulInLastTwoMinutes), s => s.TeamFoulsInLastTwoMinutes[Sides.Opponent(s.TeamInPossession)] >= FreeThrows.PenaltyFoulInLastTwoMinutes ? 1 : 0);

    public static readonly Feature<NbaState> ActionThree =
        new(nameof(ActionThree), s => s.Action == ActionType.ThreePointAttempt ? 1 : 0);

    public static readonly Feature<NbaState> ActionTurnover =
        new(nameof(ActionTurnover), s => s.Action == ActionType.Turnover ? 1 : 0);

    public static readonly Feature<NbaState> ActionFoul =
        new(nameof(ActionFoul), s => s.Action == ActionType.NonShootingFoul ? 1 : 0);

    public static readonly IReadOnlyList<Feature<NbaState>> AfterPlayStart =
        [.. Enum.GetValues<PlayStart>().Select(IndicatorOf).OfType<PlayStart>().Distinct().Select(After)];

    public static readonly IReadOnlyList<Feature<NbaState>> PreviousAwarded =
        [.. AwardedFreeThrows.Categories.Select(c => LastAwardedIn($"Previous{AwardedFreeThrows.Names[c]}", [c]))];

    public static readonly IReadOnlyList<Feature<NbaState>> KindAwarded =
        [.. AwardedFreeThrows.Kinds.Where(k => k != ReferenceAwardedKind).Select(k => LastAwardedIn($"Awarded{k}", [.. AwardedFreeThrows.Categories.Where(c => AwardedFreeThrows.KindOf(c) == k)]))];

    private const AwardedKind ReferenceAwardedKind = AwardedKind.Technical;

    private static PlayStart? IndicatorOf(PlayStart start) => start switch
    {
        PlayStart.NotSet or PlayStart.DefensiveRebound => null,
        PlayStart.PeriodStart => PlayStart.PeriodStart,
        PlayStart.OffensiveRebound => PlayStart.OffensiveRebound,
        PlayStart.MadeShot => PlayStart.MadeShot,
        PlayStart.MadeFreeThrow => PlayStart.MadeFreeThrow,
        PlayStart.Turnover => PlayStart.Turnover,
        PlayStart.SideOut => PlayStart.SideOut,
        PlayStart.AwardedBall => PlayStart.AwardedBall,
        _ => throw new ArgumentOutOfRangeException(nameof(start), start, "A play start needs an indicator, or the reference level"),
    };

    private static Feature<NbaState> After(PlayStart indicator) =>
        Indicator($"After{indicator}", nameof(NbaState.Start), Enum.GetValues<PlayStart>().Where(p => IndicatorOf(p) == indicator).Cast<object>());

    private static (Feature<NbaState> Inside, Feature<NbaState> Clock) PeriodTimeoutsSoFar(string name, int fewest, int most) =>
    (
        new($"{name}PeriodTimeouts", s => s.Period < Periods.Regulation && s.TimeoutsThisPeriod[Sides.Home] + s.TimeoutsThisPeriod[Sides.Away] >= fewest && s.TimeoutsThisPeriod[Sides.Home] + s.TimeoutsThisPeriod[Sides.Away] <= most ? 1 : 0),
        new($"{name}PeriodTimeoutsClock", s => s.Period < Periods.Regulation && s.TimeoutsThisPeriod[Sides.Home] + s.TimeoutsThisPeriod[Sides.Away] >= fewest && s.TimeoutsThisPeriod[Sides.Home] + s.TimeoutsThisPeriod[Sides.Away] <= most ? s.PeriodClock : 0)
    );

    private static Feature<NbaState> LastAwardedIn(string name, int[] categories) =>
        Indicator(name, nameof(NbaState.LastAwarded), categories.Cast<object>());

    private static Feature<NbaState> Indicator(string name, string property, IEnumerable<object> values)
    {
        var state = Expression.Parameter(typeof(NbaState), "s");
        var value = Expression.Property(state, property);
        var any = values
            .Select(v => (Expression)Expression.Equal(value, Expression.Constant(v, value.Type)))
            .Aggregate(Expression.OrElse);

        return new(name, Expression.Lambda<Func<NbaState, double>>(Expression.Condition(any, Expression.Constant(1.0), Expression.Constant(0.0)), state));
    }
}
