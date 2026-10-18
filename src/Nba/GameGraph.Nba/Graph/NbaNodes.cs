using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    public static readonly NodeSpec<NbaState, INbaIncidentCollector> Possession =
        new(nameof(Possession), [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate], NbaTransitions.Possession);
}
