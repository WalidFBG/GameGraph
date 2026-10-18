using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaNodes
{
    public static readonly NodeSpec<NbaState> Possession =
        new(nameof(Possession), [NbaFeatures.ScoreMargin, NbaFeatures.TeamScoringRate], NbaTransitions.Possession);
}
