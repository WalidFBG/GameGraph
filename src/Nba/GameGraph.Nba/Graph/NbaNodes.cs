using GameGraph.Engine;
using GameGraph.ModelFamilies;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    public static readonly NodeSpec<NbaState, INbaIncidentCollector> ActionType = Node(
        nameof(ActionType),
        new Categorical(ActionTypes.Count),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.DefenceTeamFouls],
        NbaTransitions.ActionType);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> FouledOnShot = Node(
        nameof(FouledOnShot),
        new Bernoulli(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.ShotIsThree],
        NbaTransitions.FouledOnShot);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> ShotMade = Node(
        nameof(ShotMade),
        new Bernoulli(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.ShotIsThree, NbaFeatures.ShotFouled],
        NbaTransitions.ShotMade);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> InBonus = Node(
        nameof(InBonus),
        new Bernoulli(),
        [NbaFeatures.DefenceTeamFouls, NbaFeatures.Period],
        NbaTransitions.InBonus);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> FreeThrowMade = Node(
        nameof(FreeThrowMade),
        new Bernoulli(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.LastFreeThrow],
        NbaTransitions.FreeThrowMade);

    public static readonly NodeSpec<NbaState, INbaIncidentCollector> OffensiveRebound = Node(
        nameof(OffensiveRebound),
        new Bernoulli(),
        [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate, NbaFeatures.MissedThree, NbaFeatures.MissedFreeThrow],
        NbaTransitions.OffensiveRebound);

    private static NodeSpec<NbaState, INbaIncidentCollector> Node(
        string name,
        IModelFamily family,
        Feature<NbaState>[] features,
        Transition<NbaState, INbaIncidentCollector> transition) =>
        new(name, features, family, features.Select(f => (ITerm)new LinearTerm(f.Name)).ToArray(), transition);
}
