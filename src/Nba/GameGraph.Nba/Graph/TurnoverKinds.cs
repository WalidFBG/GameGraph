namespace GameGraph.Nba.Graph;

public static class TurnoverKinds
{
    public const int Other = 0;
    public const int Team = 1;
    public const int OffensiveFoul = 2;

    public static readonly string[] Names = [nameof(Other), nameof(Team), nameof(OffensiveFoul)];
}
