using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaFeatures
{
    public static readonly Feature<NbaState> ScoreMargin =
        new(nameof(ScoreMargin), s => s.Score[s.Side] - s.Score[Sides.Opponent(s.Side)]);

    public static readonly Feature<NbaState> TeamScoringRate =
        new(nameof(TeamScoringRate), s => s.ScoringRate[s.Side]);
}
