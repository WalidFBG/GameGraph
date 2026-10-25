using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public readonly record struct AwardedFreeThrow(int Team, bool Made, AwardedKind Kind)
{
    public bool MovesTheBall => AwardedFreeThrows.MovesTheBall(Kind);
}
