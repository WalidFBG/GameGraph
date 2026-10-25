namespace GameGraph.Contract;

internal static class Utc
{
    public static DateTime Require(DateTime moment) =>
        moment.Kind == DateTimeKind.Utc ? moment : throw new ArgumentException($"{moment:O} is not in UTC", nameof(moment));
}
