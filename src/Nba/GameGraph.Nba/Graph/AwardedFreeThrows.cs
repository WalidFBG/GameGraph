namespace GameGraph.Nba.Graph;

public static class AwardedFreeThrows
{
    public const int None = 0;

    private static readonly (AwardedKind Kind, bool ToDefence)[] Awarded =
    [
        (AwardedKind.Technical, false),
        (AwardedKind.Technical, true),
        (AwardedKind.Flagrant, false),
        (AwardedKind.Flagrant, true),
        (AwardedKind.ClearPath, false),
        (AwardedKind.TakeFoul, false),
        (AwardedKind.TakeFoul, true),
    ];

    public static string[] Names { get; } = [nameof(None), .. Awarded.Select(a => $"{a.Kind}{(a.ToDefence ? "Defence" : "Offence")}")];

    public static AwardedKind[] Kinds { get; } = [.. Awarded.Select(a => a.Kind).Distinct()];

    public static IEnumerable<int> Categories => Enumerable.Range(1, Awarded.Length);

    public static int? CategoryOf(AwardedKind kind, bool toDefence)
    {
        var index = Array.IndexOf(Awarded, (kind, toDefence));

        return index < 0 ? null : index + 1;
    }

    public static AwardedKind KindOf(int category) => Of(category).Kind;

    public static bool ToDefence(int category) => Of(category).ToDefence;

    public static bool MovesTheBall(AwardedKind kind) => kind is AwardedKind.Flagrant or AwardedKind.ClearPath;

    private static (AwardedKind Kind, bool ToDefence) Of(int category)
    {
        if (category < 1 || category > Awarded.Length)
            throw new ArgumentOutOfRangeException(nameof(category), category, "Not an awarded free throw");

        return Awarded[category - 1];
    }
}
