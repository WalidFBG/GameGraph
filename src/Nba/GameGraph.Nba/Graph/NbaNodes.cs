using GameGraph.Engine;
using GameGraph.Models.Contract;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    public static readonly NodeSpec<NbaState> JumpBall = new(
        nameof(JumpBall),
        new BernoulliDistribution(),
        new Dictionary<string, FeatureTerm<NbaState>[]> { ["probability"] = [] },
        new Dictionary<string, Feature<NbaState>>(),
        NbaTransitions.JumpBall);

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
            FeatureTerm.Linear(NbaFeatures.PeriodClockExpired),
            FeatureTerm.Linear(NbaFeatures.ScoreMargin),
            FeatureTerm.Linear(NbaFeatures.Period),
            FeatureTerm.Linear(NbaFeatures.AfterMiss),
            .. LateSituation(),
            .. NbaFeatures.LateMarginByClock((0, 0), (1, 6), (7, 12), (13, 24), (25, Periods.LastTwoMinutes)).Select(FeatureTerm.Linear),
        ],
        NbaTransitions.PeriodRunsOut);

    public static readonly NodeSpec<NbaState> ActionType = Node(
        nameof(ActionType),
        new CategoricalDistribution(ActionTypes.Names),
        [
            .. new[] { NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.DefenceTeamFouls, NbaFeatures.ShotClock, NbaFeatures.PeriodClock, NbaFeatures.AtHome }.Select(FeatureTerm.Linear),
            .. LateSituation(),
            FeatureTerm.Spline(NbaFeatures.LatePeriodClock, 6),
            FeatureTerm.Linear(NbaFeatures.LateClockBinds),
            .. NbaFeatures.LateMarginByClock((1, 12), (13, 24), (25, 36), (37, 48), (49, 60), (61, 90), (91, Periods.LastTwoMinutes)).Select(FeatureTerm.Linear),
        ],
        NbaTransitions.ActionType);

    public static readonly NodeSpec<NbaState> ActionTime = new(
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
        new[] { NbaFeatures.ActionThree, NbaFeatures.ActionTurnover, NbaFeatures.ActionFoul, NbaFeatures.LateFoul }
            .Concat(NbaFeatures.AfterPlayStart)
            .Append(NbaFeatures.AtHome)
            .Select(FeatureTerm.Linear);

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
        new(name, distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, Feature<NbaState>>(), transition);
}
