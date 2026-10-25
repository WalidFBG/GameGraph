namespace GameGraph.Nba.Observed;

public readonly record struct AwardedFreeThrow(int Team, bool Made, AwardedKind Kind)
{
    public bool MovesTheBall => Kind is AwardedKind.Flagrant or AwardedKind.ClearPath;
}
