using GameGraph.Engine;
using GameGraph.Models.Contract;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    public static readonly NodeSpec<NbaState, INbaIncidentCollector> ActionType = Node(
        nameof(ActionType),
        new CategoricalDistribution(ActionTypes.Names),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.DefenceTeamFouls],
        NbaTransitions.ActionType);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> FouledOnShot = Node(
        nameof(FouledOnShot),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.ShotIsThree],
        NbaTransitions.FouledOnShot);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> ShotMade = Node(
        nameof(ShotMade),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.ShotIsThree, NbaFeatures.ShotFouled],
        NbaTransitions.ShotMade);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> InBonus = Node(
        nameof(InBonus),
        new BernoulliDistribution(),
        [NbaFeatures.DefenceTeamFouls, NbaFeatures.Period],
        NbaTransitions.InBonus);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> FreeThrowMade = Node(
        nameof(FreeThrowMade),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.LastFreeThrow],
        NbaTransitions.FreeThrowMade);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> OffensiveRebound = Node(
        nameof(OffensiveRebound),
        new BernoulliDistribution(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.MissedThree, NbaFeatures.MissedFreeThrow],
        NbaTransitions.OffensiveRebound);

    private static NodeSpec<NbaState, INbaIncidentCollector> Node(
        string name,
        DistributionDeclaration distribution,
        Feature<NbaState>[] features,
        Transition<NbaState, INbaIncidentCollector> transition)
    {
        Term[] terms = [.. features.Select(f => new LinearTerm(f.Name))];
        var model = new ModelDeclaration(distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, string>());

        return new(name, features, model, transition);
    }
}
