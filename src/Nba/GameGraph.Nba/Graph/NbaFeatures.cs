using System.Linq.Expressions;
using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaFeatures
{
    public const int LateSeconds = 120;

    private const double RegulationGameSeconds = Periods.Regulation * Periods.RegulationSeconds;

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

    public static readonly Feature<NbaState> OnePlayer =
        new(nameof(OnePlayer), _ => 1);

    public static readonly CandidateFeature<NbaState> ShotShare =
        new(nameof(ShotShare), (s, player) => s.ShotPoints == 3 ? s.LogShares[(int)PlayerEvent.ThreePointAttempt][player] : s.LogShares[(int)PlayerEvent.TwoPointAttempt][player]);

    public static readonly CandidateFeature<NbaState> ReboundShare =
        new(nameof(ReboundShare), (s, player) => s.ReboundToOffence ? s.LogShares[(int)PlayerEvent.OffensiveRebound][player] : s.LogShares[(int)PlayerEvent.DefensiveRebound][player]);

    public static readonly CandidateFeature<NbaState> ReboundsOwnMiss =
        new(nameof(ReboundsOwnMiss), (s, player) => s.ReboundToOffence && player == s.Shooter ? 1 : 0);

    public static readonly CandidateFeature<NbaState> TurnoverShare =
        new(nameof(TurnoverShare), (s, player) => s.TurnoverKind == TurnoverKinds.OffensiveFoul ? s.LogShares[(int)PlayerEvent.OffensiveFoul][player] : s.LogShares[(int)PlayerEvent.Turnover][player]);

    public static readonly CandidateFeature<NbaState> FoulShare =
        new(nameof(FoulShare), (s, player) => s.LogShares[(int)PlayerEvent.Foul][player]);

    public static readonly CandidateFeature<NbaState> PersonalFouls =
        new(nameof(PersonalFouls), (s, player) => s.PersonalFouls[player]);

    public static readonly CandidateFeature<NbaState> InFoulTrouble =
        new(nameof(InFoulTrouble), (s, player) => s.IsInFoulTrouble(player) ? 1 : 0);

    public static readonly CandidateFeature<NbaState> AssistShare =
        new(nameof(AssistShare), (s, player) => s.LogShares[(int)PlayerEvent.Assist][player]);

    public static readonly CandidateFeature<NbaState> StealShare =
        new(nameof(StealShare), (s, player) => s.LogShares[(int)PlayerEvent.Steal][player]);

    public static readonly CandidateFeature<NbaState> BlockShare =
        new(nameof(BlockShare), (s, player) => s.LogShares[(int)PlayerEvent.Block][player]);

    public static readonly Feature<NbaState> ShooterAssistedRate =
        new(nameof(ShooterAssistedRate), s => s.AssistedRates[s.Shooter]);

    public static readonly Feature<NbaState> ShooterBlockedRate =
        new(nameof(ShooterBlockedRate), s => s.BlockedRates[s.Shooter]);

    public static readonly CandidateFeature<NbaState> FoulDrawnShare =
        new(nameof(FoulDrawnShare), (s, player) => s.LogShares[(int)PlayerEvent.FoulDrawn][player]);

    public static readonly Feature<NbaState> ShotFouled =
        new(nameof(ShotFouled), s => s.Fouled ? 1 : 0);

    public static readonly Feature<NbaState> LastFreeThrow =
        new(nameof(LastFreeThrow), s => s.FreeThrowsLeft == 1 ? 1 : 0);

    public static readonly Feature<NbaState> MissedThree =
        new(nameof(MissedThree), s => s.LastMiss == Miss.Three ? 1 : 0);

    public static readonly Feature<NbaState> MissedFreeThrow =
        new(nameof(MissedFreeThrow), s => s.LastMiss == Miss.FreeThrow ? 1 : 0);

    public static readonly Feature<NbaState> ReboundToOffence =
        new(nameof(ReboundToOffence), s => s.ReboundToOffence ? 1 : 0);

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
        new(nameof(BeforeFreeThrow), s => s.Moment == Moment.FreeThrow ? 1 : 0);

    public static readonly Feature<NbaState> BetweenFreeThrows =
        new(nameof(BetweenFreeThrows), s => s.BetweenFreeThrows ? 1 : 0);

    public static readonly Feature<NbaState> TimeoutCalledHere =
        new(nameof(TimeoutCalledHere), s => s.TimeoutCalledHere ? 1 : 0);

    public static readonly Feature<NbaState> LogOffenceLongestStint =
        new(nameof(LogOffenceLongestStint), s => Math.Log(s.LongestStint(s.TeamInPossession) + 1));

    public static readonly Feature<NbaState> LogDefenceLongestStint =
        new(nameof(LogDefenceLongestStint), s => Math.Log(s.LongestStint(Sides.Opponent(s.TeamInPossession)) + 1));

    public static readonly Feature<NbaState> FiveStarters =
        new(nameof(FiveStarters), _ => Lineups.Size);

    public static readonly Feature<NbaState> OneCount =
        new(nameof(OneCount), _ => 1);

    public static readonly Feature<NbaState> ChangeSize =
        new(nameof(ChangeSize), s => s.ChangeSize);

    public static readonly Feature<NbaState> ChosenOff =
        new(nameof(ChosenOff), s => s.ChangeSize - s.ForcedOff);

    public static readonly Feature<NbaState> OffenceMustChange =
        new(nameof(OffenceMustChange), s => s.MustChange(s.TeamInPossession) ? 1 : 0);

    public static readonly Feature<NbaState> DefenceInFoulTrouble =
        new(nameof(DefenceInFoulTrouble), s => s.InFoulTrouble(Sides.Opponent(s.TeamInPossession)));

    public static readonly Feature<NbaState> DefenceMustChange =
        new(nameof(DefenceMustChange), s => s.MustChange(Sides.Opponent(s.TeamInPossession)) ? 1 : 0);

    public static readonly CandidateFeature<NbaState> StartShare =
        new(nameof(StartShare), (s, player) => s.StartShares[player]);

    public static readonly CandidateFeature<NbaState> StartedLast =
        new(nameof(StartedLast), (s, player) => s.StartedLastGame[player]);

    public static readonly CandidateFeature<NbaState> UsualSeconds =
        new(nameof(UsualSeconds), (s, player) => s.UsualSeconds[player]);

    public static readonly CandidateFeature<NbaState> LogSinceChange =
        new(nameof(LogSinceChange), (s, player) => Math.Log(s.Elapsed - s.ChangedAt[player] + 1));

    public static readonly CandidateFeature<NbaState> PlayedAgainstUsual =
        new(nameof(PlayedAgainstUsual), (s, player) => s.SecondsOnCourt(player, s.Elapsed) - s.UsualSeconds[player] * s.Elapsed / RegulationGameSeconds);

    public static readonly CandidateFeature<NbaState> Started =
        new(nameof(Started), (s, player) => s.Started[player] ? 1 : 0);

    public static readonly CandidateFeature<NbaState> StartedLate =
        new(nameof(StartedLate), (s, player) => s.Started[player] && s.Period >= Periods.Regulation - 1 && s.PeriodClock <= LateSeconds ? 1 : 0);

    public static readonly CandidateFeature<NbaState> StartedAtPeriodStart =
        new(nameof(StartedAtPeriodStart), (s, player) => s.Started[player] && s.Start == PlayStart.PeriodStart && s.Moment == Moment.Play ? 1 : 0);

    public static readonly IReadOnlyList<CandidateFeature<NbaState>> CountIs =
        [.. Enumerable.Range(2, Lineups.Size - 1).Select(c => new CandidateFeature<NbaState>($"CountIs{c}", (_, count) => count == c ? 1 : 0))];

    public static readonly CandidateFeature<NbaState> CountAfterTimeout =
        new(nameof(CountAfterTimeout), (s, count) => s.TimeoutCalledHere ? count : 0);

    public static readonly CandidateFeature<NbaState> CountAtPeriodStart =
        new(nameof(CountAtPeriodStart), (s, count) => s.Start == PlayStart.PeriodStart && s.Moment == Moment.Play ? count : 0);

    public static readonly CandidateFeature<NbaState> CountBeforeFreeThrow =
        new(nameof(CountBeforeFreeThrow), (s, count) => s.Moment == Moment.FreeThrow ? count : 0);

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
        [.. Enum.GetValues<PlayStart>().Except([PlayStart.NotSet, PlayStart.DefensiveRebound]).Select(After)];

    public static readonly IReadOnlyList<Feature<NbaState>> PreviousAwarded =
        [.. AwardedFreeThrows.Categories.Select(c => LastAwardedIn($"Previous{AwardedFreeThrows.Names[c]}", [c]))];

    public static readonly IReadOnlyList<Feature<NbaState>> KindAwarded =
        [.. AwardedFreeThrows.Kinds.Where(k => k != ReferenceAwardedKind).Select(k => LastAwardedIn($"Awarded{k}", [.. AwardedFreeThrows.Categories.Where(c => AwardedFreeThrows.KindOf(c) == k)]))];

    public static Feature<NbaState> After(PlayStart start) =>
        Indicator($"After{start}", nameof(NbaState.Start), [start]);

    private const AwardedKind ReferenceAwardedKind = AwardedKind.Technical;

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
