using GameGraph.Engine;
using GameGraph.ModelFamilies;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    public static readonly NodeSpec<NbaState, INbaIncidentCollector> Possession =
        new(
            nameof(Possession),
            [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate],
            new Bernoulli(),
            [new LinearTerm(NbaFeatures.ScoreMargin.Name), new LinearTerm(NbaFeatures.TeamScoringRate.Name)],
            NbaTransitions.Possession);
}
