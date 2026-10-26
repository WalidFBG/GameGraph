namespace GameGraph.Nba.Graph;

public static class SubstitutingTeams
{
    public const int None = 0;
    public const int Offence = 1;
    public const int Defence = 2;
    public const int Both = Offence | Defence;

    public static readonly string[] Names = [nameof(None), nameof(Offence), nameof(Defence), nameof(Both)];

    private static readonly int[][] CountsUpTo = [.. Enumerable.Range(1, Lineups.Size).Select(most => Enumerable.Range(1, most).ToArray())];

    public static int Of(bool offence, bool defence) => (offence ? Offence : None) | (defence ? Defence : None);

    public static int[] Counts(int bench) =>
        bench > 0 ? CountsUpTo[Math.Min(bench, Lineups.Size) - 1] : throw new ArgumentOutOfRangeException(nameof(bench), bench, "A team substitutes from a bench of at least one");
}
