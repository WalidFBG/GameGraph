namespace GameGraph.Contract;

internal static class DistinctNames
{
    public static void Require(IEnumerable<string> names)
    {
        var repeated = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).Order(StringComparer.Ordinal).ToArray();

        if (repeated.Length > 0)
            throw new ArgumentException($"{string.Join(", ", repeated)} appear more than once", nameof(names));
    }
}
