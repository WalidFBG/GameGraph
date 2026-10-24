namespace GameGraph.Nba.Graph;

public static class AwardedFreeThrows
{
    public const int None = 0;

    public const int Offence = 1;

    public const int Defence = 2;

    public static string[] Names { get; } = [nameof(None), nameof(Offence), nameof(Defence)];
}
