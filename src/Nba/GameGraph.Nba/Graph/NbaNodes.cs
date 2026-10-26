using GameGraph.Contract.Distributions;
using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    private static readonly (string Name, int Lowest, int Highest)[] FoulingBands =
        [("Trail3", -3, -3), ("Lead1To3", 1, 3), ("Lead4To8", 4, 8), ("Lead9To12", 9, 12)];

    private static readonly (string Name, int Lowest, int Highest)[] RunningOutBands =
        [("Trail10OrMore", int.MinValue, -10), ("Trail4To9", -9, -4), ("Trail1To3", -3, -1), ("Lead1To3", 1, 3), ("Lead4To8", 4, 8), ("Lead9OrMore", 9, int.MaxValue)];

    public static readonly ChoiceNode<NbaState> Starters = new(
        nameof(Starters),
        s => s.Active[s.LineupTeam],
        NbaFeatures.FiveStarters,
        [CandidateTerm.Spline(NbaFeatures.StartShare, 4), CandidateTerm.Spline(NbaFeatures.UsualSeconds, 4), CandidateTerm.Linear(NbaFeatures.StartedLast)],
        NbaTransitions.Starters);

    public static readonly NodeSpec<NbaState> JumpBall = new VectorNode<NbaState>(
        nameof(JumpBall),
        new BernoulliDistribution(),
        new Dictionary<string, FeatureTerm<NbaState>[]> { ["probability"] = [] },
        new Dictionary<string, Feature<NbaState>>(),
        NbaTransitions.JumpBall);

    public static readonly NodeSpec<NbaState> OffenceTimeout = Node(
        nameof(OffenceTimeout),
        new BernoulliDistribution(),
        [.. TimeoutTerms(NbaFeatures.OffenceTimeoutsLeft, NbaFeatures.AfterPlayStart), FeatureTerm.Linear(NbaFeatures.ScoreMargin), .. LateSituation()],
        NbaTransitions.OffenceTimeout);

    public static readonly NodeSpec<NbaState> DefenceTimeout = Node(
        nameof(DefenceTimeout),
        new BernoulliDistribution(),
        TimeoutTerms(
            NbaFeatures.DefenceTimeoutsLeft,
            [.. new[] { PlayStart.PeriodStart, PlayStart.OffensiveRebound, PlayStart.MadeShot, PlayStart.MadeFreeThrow, PlayStart.DeadBallTurnover, PlayStart.SideOut, PlayStart.AwardedBall, PlayStart.TeamOffensiveRebound, PlayStart.Steal }.Select(NbaFeatures.After)]),
        NbaTransitions.DefenceTimeout);

    public static readonly NodeSpec<NbaState> Substitutions = Node(
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
        nameof(SubstitutionCount),
        s => SubstitutingTeams.Counts(s.ForcedOff, s.Bench[s.LineupTeam].Length),
        NbaFeatures.OneCount,
        [
            .. NbaFeatures.CountIs.Select(CandidateTerm.Linear),
            .. new[] { NbaFeatures.CountAfterTimeout, NbaFeatures.CountAtPeriodStart, NbaFeatures.CountBeforeFreeThrow }.Select(CandidateTerm.Linear),
        ],
        NbaTransitions.SubstitutionCount);

    public static readonly ChoiceNode<NbaState> SubstitutedOff = new(
        nameof(SubstitutedOff),
        s => s.Removable,
        NbaFeatures.ChosenOff,
        [.. PlayerTerms(), CandidateTerm.Linear(NbaFeatures.StartedLate)],
        NbaTransitions.SubstitutedOff);

    public static readonly ChoiceNode<NbaState> SubstitutedOn = new(
        nameof(SubstitutedOn),
        s => s.Bench[s.LineupTeam],
        NbaFeatures.ChangeSize,
        PlayerTerms(),
        NbaTransitions.SubstitutedOn);

    public static readonly NodeSpec<NbaState> AwardedFreeThrow = Node(
        nameof(AwardedFreeThrow),
        new CategoricalDistribution(AwardedFreeThrows.Names),
        [NbaFeatures.ScoreMargin, NbaFeatures.LateInGame, NbaFeatures.AtHome, .. NbaFeatures.PreviousAwarded],
        NbaTransitions.AwardedFreeThrow);

    public static readonly NodeSpec<NbaState> AwardedFreeThrowMade = Node(
        nameof(AwardedFreeThrowMade),
        new BernoulliDistribution(),
        [NbaFeatures.ShooterMargin, NbaFeatures.ShooterPointsPerPossession, NbaFeatures.ShooterAtHome, .. NbaFeatures.KindAwarded],
        NbaTransitions.AwardedFreeThrowMade);

    public static readonly NodeSpec<NbaState> PeriodRunsOut = Node(
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

    public static readonly NodeSpec<NbaState> ActionType = Node(
        nameof(ActionType),
        new CategoricalDistribution(ActionTypes.Names),
        [
            .. new[] { NbaFeatures.TeamPointsPerPossession, NbaFeatures.DefenceTeamFouls, NbaFeatures.ShotClock, NbaFeatures.PeriodClock }.Select(FeatureTerm.Linear),
            .. LateSituation(),
            FeatureTerm.Spline(NbaFeatures.LatePeriodClock, 6),
            .. ClockByBand(FoulingBands, 4),
            .. new[] { PlayStart.OffensiveRebound, PlayStart.TeamDefensiveRebound, PlayStart.TeamOffensiveRebound, PlayStart.Steal }.Select(NbaFeatures.After).Select(FeatureTerm.Linear),
        ],
        NbaTransitions.ActionType);

    public static readonly NodeSpec<NbaState> ActionTime = new VectorNode<NbaState>(
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

    public static readonly NodeSpec<NbaState> TurnoverKind = Node(
        nameof(TurnoverKind),
        new CategoricalDistribution(TurnoverKinds.Names),
        [FeatureTerm.Spline(NbaFeatures.ShotClock, 24), FeatureTerm.Spline(NbaFeatures.PeriodClock, 10)],
        NbaTransitions.TurnoverKind);

    public static readonly ChoiceNode<NbaState> TurnedOverBy = new(
        nameof(TurnedOverBy),
        s => s.OnCourt[s.TeamInPossession],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.TurnoverShare, 4)],
        NbaTransitions.TurnedOverBy);

    public static readonly NodeSpec<NbaState> Stolen = Node(
        nameof(Stolen),
        new BernoulliDistribution(),
        [
            FeatureTerm.Spline(NbaFeatures.ShotClock, 8),
            FeatureTerm.Linear(NbaFeatures.ScoreMargin),
            FeatureTerm.Linear(NbaFeatures.LateInGame),
            .. new[] { PlayStart.MadeShot, PlayStart.SideOut, PlayStart.TeamOffensiveRebound, PlayStart.Steal }.Select(NbaFeatures.After).Select(FeatureTerm.Linear),
        ],
        NbaTransitions.Stolen);

    public static readonly ChoiceNode<NbaState> Shooter = new(
        nameof(Shooter),
        s => s.OnCourt[s.TeamInPossession],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.ShotShare, 4)],
        NbaTransitions.Shooter);

    public static readonly ChoiceNode<NbaState> Fouler = new(
        nameof(Fouler),
        s => s.OnCourt[Sides.Opponent(s.TeamInPossession)],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.FoulShare, 4), CandidateTerm.Linear(NbaFeatures.PersonalFouls), CandidateTerm.Linear(NbaFeatures.InFoulTrouble)],
        NbaTransitions.Fouler);

    public static readonly ChoiceNode<NbaState> FouledPlayer = new(
        nameof(FouledPlayer),
        s => s.OnCourt[s.TeamInPossession],
        NbaFeatures.OnePlayer,
        [CandidateTerm.Spline(NbaFeatures.FoulDrawnShare, 4)],
        NbaTransitions.FouledPlayer);

    public static readonly NodeSpec<NbaState> FouledOnShot = Node(
        nameof(FouledOnShot),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.ShotIsThree, NbaFeatures.AtHome],
        NbaTransitions.FouledOnShot);

    public static readonly NodeSpec<NbaState> ShotMade = Node(
        nameof(ShotMade),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.ShotIsThree, NbaFeatures.ShotFouled, NbaFeatures.AtHome],
        NbaTransitions.ShotMade);

    public static readonly NodeSpec<NbaState> InBonus = Node(
        nameof(InBonus),
        new BernoulliDistribution(),
        [NbaFeatures.DefenceInPenalty, NbaFeatures.DefenceSecondFoulInLastTwoMinutes],
        NbaTransitions.InBonus);

    public static readonly NodeSpec<NbaState> FreeThrowMade = Node(
        nameof(FreeThrowMade),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.LastFreeThrow, NbaFeatures.AtHome],
        NbaTransitions.FreeThrowMade);

    public static readonly NodeSpec<NbaState> OffensiveRebound = Node(
        nameof(OffensiveRebound),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.MissedThree, NbaFeatures.MissedFreeThrow, NbaFeatures.AtHome],
        NbaTransitions.OffensiveRebound);

    public static readonly NodeSpec<NbaState> TeamRebound = Node(
        nameof(TeamRebound),
        new BernoulliDistribution(),
        [NbaFeatures.ReboundToOffence, NbaFeatures.MissedFreeThrow],
        NbaTransitions.TeamRebound);

    public static readonly ChoiceNode<NbaState> Rebounder = new(
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

    private static NodeSpec<NbaState> Node(
        string name,
        DistributionDeclaration distribution,
        Feature<NbaState>[] features,
        Transition<NbaState> transition) =>
        Node(name, distribution, [.. features.Select(FeatureTerm.Linear)], transition);

    private static NodeSpec<NbaState> Node(
        string name,
        DistributionDeclaration distribution,
        FeatureTerm<NbaState>[] terms,
        Transition<NbaState> transition) =>
        new VectorNode<NbaState>(name, distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, Feature<NbaState>>(), transition);
}
