using GameGraph.Contract.Distributions;
using GameGraph.Engine.Nodes;
using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    internal static readonly GraphBuilder<NbaState> Builder = new();

    private static readonly (string Name, int Lowest, int Highest)[] FoulingBands =
        [("Trail3", -3, -3), ("Lead1To3", 1, 3), ("Lead4To8", 4, 8), ("Lead9To12", 9, 12)];

    private static readonly (string Name, int Lowest, int Highest)[] RunningOutBands =
        [("Trail10OrMore", int.MinValue, -10), ("Trail4To9", -9, -4), ("Trail1To3", -3, -1), ("Lead1To3", 1, 3), ("Lead4To8", 4, 8), ("Lead9OrMore", 9, int.MaxValue)];

    public static readonly ChoiceNode<NbaState> Starters = new(
        Builder,
        nameof(Starters),
        s => s.Active[s.LineupTeam],
        NbaFeatures.FiveStarters,
        [CandidateTerm.Spline(NbaFeatures.StartShare, 4), CandidateTerm.Spline(NbaFeatures.UsualSeconds, 4), CandidateTerm.Linear(NbaFeatures.StartedLast)],
        NbaTransitions.Starters);

    public static readonly Node<NbaState> JumpBall = new SingleRowNode<NbaState>(
        Builder,
        nameof(JumpBall),
        new BernoulliDistribution(),
        new Dictionary<string, FeatureTerm<NbaState>[]> { ["probability"] = [] },
        new Dictionary<string, Feature<NbaState>>(),
        NbaTransitions.JumpBall);

    public static readonly Node<NbaState> OffenceTimeout = SingleRow(
        nameof(OffenceTimeout),
        new BernoulliDistribution(),
        [.. TimeoutTerms(NbaFeatures.OffenceTimeoutsLeft, NbaFeatures.AfterPlayStart), FeatureTerm.Linear(NbaFeatures.ScoreMargin), .. LateSituation()],
        NbaTransitions.OffenceTimeout);

    public static readonly Node<NbaState> DefenceTimeout = SingleRow(
        nameof(DefenceTimeout),
        new BernoulliDistribution(),
        TimeoutTerms(
            NbaFeatures.DefenceTimeoutsLeft,
            [.. new[] { PlayStart.PeriodStart, PlayStart.OffensiveRebound, PlayStart.MadeShot, PlayStart.MadeFreeThrow, PlayStart.DeadBallTurnover, PlayStart.SideOut, PlayStart.AwardedBall, PlayStart.TeamOffensiveRebound, PlayStart.Steal }.Select(NbaFeatures.After)]),
        NbaTransitions.DefenceTimeout);

    public static readonly Node<NbaState> Substitutions = SingleRow(
        nameof(Substitutions),
        new CategoricalDistribution(SubstitutingTeams.Names),
        [
            .. new[] { NbaFeatures.BeforeFreeThrow, NbaFeatures.BetweenFreeThrows, NbaFeatures.TimeoutCalledHere, NbaFeatures.Period, NbaFeatures.LateInGame, NbaFeatures.OffenceMustChange, NbaFeatures.DefenceMustChange, NbaFeatures.DefenceInFoulTrouble }
                .Concat(new[] { PlayStart.PeriodStart, PlayStart.MadeShot, PlayStart.DeadBallTurnover, PlayStart.SideOut, PlayStart.TeamDefensiveRebound, PlayStart.TeamOffensiveRebound }.Select(NbaFeatures.After))
                .Select(FeatureTerm.Linear),
            FeatureTerm.Spline(NbaFeatures.PeriodClock, 4),
            FeatureTerm.Spline(NbaFeatures.LogOffenceLongestStint, 6),
            FeatureTerm.Spline(NbaFeatures.LogDefenceLongestStint, 6),
        ],
        NbaTransitions.Substitutions);

    public static readonly ChoiceNode<NbaState> SubstitutionCount = new(
        Builder,
        nameof(SubstitutionCount),
        s => SubstitutingTeams.Counts(s.ForcedOff, s.Bench[s.LineupTeam].Length),
        NbaFeatures.OneCount,
        [
            .. NbaFeatures.CountIs.Select(CandidateTerm.Linear),
            .. new[] { NbaFeatures.CountAfterTimeout, NbaFeatures.CountAtPeriodStart, NbaFeatures.CountBeforeFreeThrow }.Select(CandidateTerm.Linear),
        ],
        NbaTransitions.SubstitutionCount);

    public static readonly ChoiceNode<NbaState> SubstitutedOff = new(
        Builder,
        nameof(SubstitutedOff),
        s => s.Removable,
        NbaFeatures.ChosenOff,
        [.. PlayerTerms(), CandidateTerm.Linear(NbaFeatures.StartedLate)],
        NbaTransitions.SubstitutedOff);

    public static readonly ChoiceNode<NbaState> SubstitutedOn = new(
        Builder,
        nameof(SubstitutedOn),
        s => s.Bench[s.LineupTeam],
        NbaFeatures.ChangeSize,
        PlayerTerms(),
        NbaTransitions.SubstitutedOn);

    public static readonly Node<NbaState> AwardedFreeThrow = SingleRow(
        nameof(AwardedFreeThrow),
        new CategoricalDistribution(AwardedFreeThrows.Names),
        [NbaFeatures.ScoreMargin, NbaFeatures.LateInGame, NbaFeatures.AtHome, .. NbaFeatures.PreviousAwarded],
        NbaTransitions.AwardedFreeThrow);

    public static readonly ChoiceNode<NbaState> AwardedShooter = new(
        Builder,
        nameof(AwardedShooter),
        s => s.AwardedShooters,
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.AwardedShare, 4), CandidateTerm.Linear(NbaFeatures.TookPreviousAwarded), CandidateTerm.Linear(NbaFeatures.OnCourtNow)],
        NbaTransitions.AwardedShooter);

    public static readonly ChoiceNode<NbaState> AwardedFouler = new(
        Builder,
        nameof(AwardedFouler),
        s => s.AwardedFoulers,
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.FoulShare, 4), CandidateTerm.Linear(NbaFeatures.InFoulTrouble), CandidateTerm.Linear(NbaFeatures.OnCourtNow)],
        NbaTransitions.AwardedFouler);

    public static readonly Node<NbaState> AwardedFreeThrowMade = SingleRow(
        nameof(AwardedFreeThrowMade),
        new BernoulliDistribution(),
        [.. new[] { NbaFeatures.ShooterMargin, NbaFeatures.ShooterPointsPerPossession, NbaFeatures.ShooterAtHome }.Concat(NbaFeatures.KindAwarded).Select(FeatureTerm.Linear), FeatureTerm.Spline(NbaFeatures.AwardedShooterFreeThrowRate, 4)],
        NbaTransitions.AwardedFreeThrowMade);

    public static readonly Node<NbaState> PeriodRunsOut = SingleRow(
        nameof(PeriodRunsOut),
        new BernoulliDistribution(),
        [
            FeatureTerm.Spline(NbaFeatures.PeriodClock, 6),
            FeatureTerm.Linear(NbaFeatures.Period),
            FeatureTerm.Linear(NbaFeatures.AfterMiss),
            .. LateSituation(),
            .. ClockByBand(RunningOutBands, 3),
        ],
        NbaTransitions.PeriodRunsOut);

    public static readonly Node<NbaState> ActionType = SingleRow(
        nameof(ActionType),
        new CategoricalDistribution(ActionTypes.Names),
        [
            .. new[] { NbaFeatures.TeamPointsPerPossession, NbaFeatures.DefenceTeamFouls, NbaFeatures.ShotClock, NbaFeatures.PeriodClock }.Select(FeatureTerm.Linear),
            .. LateSituation(),
            FeatureTerm.Spline(NbaFeatures.LatePeriodClock, 6),
            .. ClockByBand(FoulingBands, 4),
            .. new[] { PlayStart.OffensiveRebound, PlayStart.TeamDefensiveRebound, PlayStart.TeamOffensiveRebound, PlayStart.Steal }.Select(NbaFeatures.After).Select(FeatureTerm.Linear),
            FeatureTerm.Spline(NbaFeatures.DefenceStealRate, 4),
            FeatureTerm.Spline(NbaFeatures.OffenceTwoRate, 4),
            FeatureTerm.Spline(NbaFeatures.OffenceThreeRate, 4),
            FeatureTerm.Spline(NbaFeatures.OffenceFoulDrawnRate, 4),
        ],
        NbaTransitions.ActionType);

    public static readonly Node<NbaState> ActionTime = new SingleRowNode<NbaState>(
        Builder,
        nameof(ActionTime),
        new DiscreteJohnsonSuDistribution(),
        new Dictionary<string, FeatureTerm<NbaState>[]>
        {
            ["location"] = ActionTimeShape(),
            ["scale"] = ActionTimeShape(),
            ["skew"] = ActionTimeSkewAndTail(),
            ["tail"] = ActionTimeSkewAndTail(),
        },
        new Dictionary<string, Feature<NbaState>> { ["maximum"] = NbaFeatures.Bound },
        NbaTransitions.ActionTime);

    public static readonly Node<NbaState> TurnoverKind = SingleRow(
        nameof(TurnoverKind),
        new CategoricalDistribution(TurnoverKinds.Names),
        [FeatureTerm.Spline(NbaFeatures.ShotClock, 24), FeatureTerm.Spline(NbaFeatures.PeriodClock, 10)],
        NbaTransitions.TurnoverKind);

    public static readonly ChoiceNode<NbaState> TurnedOverBy = new(
        Builder,
        nameof(TurnedOverBy),
        s => s.OnCourt[s.TeamInPossession],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.TurnoverShare, 4)],
        NbaTransitions.TurnedOverBy);

    public static readonly Node<NbaState> Stolen = SingleRow(
        nameof(Stolen),
        new BernoulliDistribution(),
        [
            FeatureTerm.Spline(NbaFeatures.ShotClock, 8),
            FeatureTerm.Linear(NbaFeatures.ScoreMargin),
            FeatureTerm.Linear(NbaFeatures.LateInGame),
            .. new[] { PlayStart.MadeShot, PlayStart.SideOut, PlayStart.TeamOffensiveRebound, PlayStart.Steal }.Select(NbaFeatures.After).Select(FeatureTerm.Linear),
            FeatureTerm.Spline(NbaFeatures.DefenceStealRate, 4),
        ],
        NbaTransitions.Stolen);

    public static readonly ChoiceNode<NbaState> Stealer = new(
        Builder,
        nameof(Stealer),
        s => s.OnCourt[Sides.Opponent(s.TeamInPossession)],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.StealShare, 4)],
        NbaTransitions.Stealer);

    public static readonly ChoiceNode<NbaState> Shooter = new(
        Builder,
        nameof(Shooter),
        s => s.OnCourt[s.TeamInPossession],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.ShotShare, 4)],
        NbaTransitions.Shooter);

    public static readonly Node<NbaState> FouledOnShot = SingleRow(
        nameof(FouledOnShot),
        new BernoulliDistribution(),
        [.. new[] { NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.ShotIsThree, NbaFeatures.AtHome }.Select(FeatureTerm.Linear), FeatureTerm.Spline(NbaFeatures.ShooterFouledRatio, 4), FeatureTerm.Spline(NbaFeatures.DefenceShootingFoulRate, 4)],
        NbaTransitions.FouledOnShot);

    public static readonly ChoiceNode<NbaState> Fouler = new(
        Builder,
        nameof(Fouler),
        s => s.OnCourt[Sides.Opponent(s.TeamInPossession)],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.FoulShare, 4), CandidateTerm.Linear(NbaFeatures.PersonalFouls), CandidateTerm.Linear(NbaFeatures.InFoulTrouble)],
        NbaTransitions.Fouler);

    public static readonly ChoiceNode<NbaState> FouledPlayer = new(
        Builder,
        nameof(FouledPlayer),
        s => s.OnCourt[s.TeamInPossession],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.FoulDrawnShare, 4)],
        NbaTransitions.FouledPlayer);

    public static readonly Node<NbaState> ShotMade = SingleRow(
        nameof(ShotMade),
        new BernoulliDistribution(),
        [.. new[] { NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.ShotIsThree, NbaFeatures.ShotFouled, NbaFeatures.AtHome }.Select(FeatureTerm.Linear), FeatureTerm.Spline(NbaFeatures.ShooterMakeRate, 4), FeatureTerm.Spline(NbaFeatures.DefenceBlockRate, 4)],
        NbaTransitions.ShotMade);

    public static readonly Node<NbaState> Assisted = SingleRow(
        nameof(Assisted),
        new BernoulliDistribution(),
        [
            FeatureTerm.Linear(NbaFeatures.ShotIsThree),
            FeatureTerm.Spline(NbaFeatures.ShotClock, 24),
            FeatureTerm.Spline(NbaFeatures.ShooterAssistedRate, 4),
            .. new[] { PlayStart.OffensiveRebound, PlayStart.TeamOffensiveRebound }.Select(NbaFeatures.After).Select(FeatureTerm.Linear),
        ],
        NbaTransitions.Assisted);

    public static readonly ChoiceNode<NbaState> Assister = new(
        Builder,
        nameof(Assister),
        s => s.Teammates,
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.AssistShare, 4)],
        NbaTransitions.Assister);

    public static readonly Node<NbaState> Blocked = SingleRow(
        nameof(Blocked),
        new BernoulliDistribution(),
        [FeatureTerm.Linear(NbaFeatures.ShotIsThree), FeatureTerm.Spline(NbaFeatures.ShooterBlockedRate, 4), FeatureTerm.Spline(NbaFeatures.ShotClock, 8)],
        NbaTransitions.Blocked);

    public static readonly ChoiceNode<NbaState> Blocker = new(
        Builder,
        nameof(Blocker),
        s => s.OnCourt[Sides.Opponent(s.TeamInPossession)],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.BlockShare, 4)],
        NbaTransitions.Blocker);

    public static readonly Node<NbaState> InBonus = SingleRow(
        nameof(InBonus),
        new BernoulliDistribution(),
        [NbaFeatures.DefenceInPenalty, NbaFeatures.DefenceSecondFoulInLastTwoMinutes],
        NbaTransitions.InBonus);

    public static readonly Node<NbaState> FreeThrowMade = SingleRow(
        nameof(FreeThrowMade),
        new BernoulliDistribution(),
        [.. new[] { NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.LastFreeThrow, NbaFeatures.AtHome }.Select(FeatureTerm.Linear), FeatureTerm.Spline(NbaFeatures.ShooterFreeThrowRate, 4)],
        NbaTransitions.FreeThrowMade);

    public static readonly Node<NbaState> OffensiveRebound = SingleRow(
        nameof(OffensiveRebound),
        new BernoulliDistribution(),
        [.. new[] { NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.MissedThree, NbaFeatures.MissedFreeThrow, NbaFeatures.AtHome }.Select(FeatureTerm.Linear), FeatureTerm.Spline(NbaFeatures.OffenceReboundRate, 4), FeatureTerm.Spline(NbaFeatures.DefenceReboundRate, 4)],
        NbaTransitions.OffensiveRebound);

    public static readonly Node<NbaState> TeamRebound = SingleRow(
        nameof(TeamRebound),
        new BernoulliDistribution(),
        [NbaFeatures.ReboundToOffence, NbaFeatures.MissedFreeThrow],
        NbaTransitions.TeamRebound);

    public static readonly ChoiceNode<NbaState> Rebounder = new(
        Builder,
        nameof(Rebounder),
        s => s.OnCourt[s.ReboundToOffence ? s.TeamInPossession : Sides.Opponent(s.TeamInPossession)],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.ReboundShare, 4), CandidateTerm.Linear(NbaFeatures.ReboundsOwnMiss)],
        NbaTransitions.Rebounder);

    private static CandidateTerm<NbaState>[] PlayerTerms() =>
    [
        CandidateTerm.Spline(NbaFeatures.LogSinceChange, 16),
        CandidateTerm.Spline(NbaFeatures.PlayedAgainstUsual, 8),
        CandidateTerm.Spline(NbaFeatures.UsualSeconds, 6),
        CandidateTerm.Linear(NbaFeatures.Started),
        CandidateTerm.Linear(NbaFeatures.StartedAtPeriodStart),
        CandidateTerm.Spline(NbaFeatures.PersonalFouls, Lineups.FoulOut + 1),
        CandidateTerm.Linear(NbaFeatures.InFoulTrouble),
    ];

    private static FeatureTerm<NbaState>[] ActionTimeShape() =>
    [
        .. ActionTimeSituation(),
        FeatureTerm.Spline(NbaFeatures.Bound, 8),
        FeatureTerm.Linear(NbaFeatures.PeriodClockBinds),
        FeatureTerm.Spline(NbaFeatures.ScoreMargin, 6),
        FeatureTerm.Linear(NbaFeatures.LateInGame),
    ];

    private static FeatureTerm<NbaState>[] ActionTimeSkewAndTail() =>
    [
        .. ActionTimeSituation(),
        FeatureTerm.Linear(NbaFeatures.Bound),
        FeatureTerm.Linear(NbaFeatures.LogBound),
        FeatureTerm.Linear(NbaFeatures.PeriodClockBinds),
        FeatureTerm.Linear(NbaFeatures.ScoreMargin),
        FeatureTerm.Linear(NbaFeatures.LateInGame),
    ];

    private static IEnumerable<FeatureTerm<NbaState>> ActionTimeSituation() =>
        new[] { NbaFeatures.ActionThree, NbaFeatures.ActionTurnover, NbaFeatures.ActionFoul, NbaFeatures.LateFoul, NbaFeatures.AfterTimeout, NbaFeatures.LateAfterTimeout }
            .Concat(NbaFeatures.AfterPlayStart)
            .Append(NbaFeatures.AtHome)
            .Select(FeatureTerm.Linear);

    private static FeatureTerm<NbaState>[] TimeoutTerms(Feature<NbaState> timeoutsLeft, IEnumerable<Feature<NbaState>> starts) =>
    [
        .. new[] { NbaFeatures.BeforeFreeThrow, NbaFeatures.TimeoutJustCalled, NbaFeatures.Period }
            .Concat(starts)
            .Select(FeatureTerm.Linear),
        FeatureTerm.Spline(timeoutsLeft, 4),
        .. NbaFeatures.PeriodTimeouts.SelectMany(t => new[] { FeatureTerm.Linear(t.Inside), FeatureTerm.Spline(t.Clock, 10) }),
    ];

    private static IEnumerable<FeatureTerm<NbaState>> ClockByBand((string Name, int Lowest, int Highest)[] bands, int knots) =>
        bands
            .Select(b => NbaFeatures.LateBand(b.Name, b.Lowest, b.Highest))
            .SelectMany(b => new[] { FeatureTerm.Linear(b.Inside), FeatureTerm.Spline(b.Clock, knots) });

    private static FeatureTerm<NbaState>[] LateSituation() =>
    [
        FeatureTerm.Linear(NbaFeatures.LateInGame),
        FeatureTerm.Spline(NbaFeatures.LateMargin, 8),
    ];

    private static Node<NbaState> SingleRow(
        string name,
        DistributionDeclaration distribution,
        Feature<NbaState>[] features,
        Transition<NbaState> transition) =>
        SingleRow(name, distribution, [.. features.Select(FeatureTerm.Linear)], transition);

    private static Node<NbaState> SingleRow(
        string name,
        DistributionDeclaration distribution,
        FeatureTerm<NbaState>[] terms,
        Transition<NbaState> transition) =>
        new SingleRowNode<NbaState>(Builder, name, distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, Feature<NbaState>>(), transition);
}
