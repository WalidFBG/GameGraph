using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaFeatures
{
    public static readonly Feature<NbaState> ScoreMargin =
        new(nameof(ScoreMargin), s => s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> TeamScoringRate =
        new(nameof(TeamScoringRate), s => s.ScoringRate[s.TeamInPossession]);
}
