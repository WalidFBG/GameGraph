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
        [NbaFeatures.PeriodClock, NbaFeatures.ScoreMargin, NbaFeatures.Period, NbaFeatures.AfterMiss],
        NbaTransitions.PeriodRunsOut);

    public static readonly NodeSpec<NbaState> ActionType = Node(
        nameof(ActionType),
        new CategoricalDistribution(ActionTypes.Names),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamPointsPerPossession, NbaFeatures.DefenceTeamFouls, NbaFeatures.ShotClock, NbaFeatures.PeriodClock, NbaFeatures.AtHome],
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
        [NbaFeatures.DefenceTeamFouls, NbaFeatures.Period],
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
        new[] { NbaFeatures.ActionThree, NbaFeatures.ActionTurnover, NbaFeatures.ActionFoul }
            .Concat(NbaFeatures.AfterPlayStart)
            .Append(NbaFeatures.AtHome)
            .Select(FeatureTerm.Linear);

    private static NodeSpec<NbaState> Node(
        string name,
        DistributionDeclaration distribution,
        Feature<NbaState>[] features,
        Transition<NbaState> transition)
    {
        FeatureTerm<NbaState>[] terms = [.. features.Select(FeatureTerm.Linear)];

        return new(name, distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, Feature<NbaState>>(), transition);
    }
}
