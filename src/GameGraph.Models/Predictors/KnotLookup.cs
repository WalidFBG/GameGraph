namespace GameGraph.Models.Predictors;

internal sealed record KnotLookup(int Column, double[] Knots)
{
    public void Locate(ReadOnlySpan<double> features, out int segment, out double value)
    {
        value = Math.Min(Math.Max(features[Column], Knots[0]), Knots[^1]);
        segment = 0;

        while (segment < Knots.Length - 2 && value >= Knots[segment + 1])
            segment++;
    }

    public static int IndexIn(List<KnotLookup> lookups, int column, double[] knots)
    {
        var index = lookups.FindIndex(l => l.Column == column && l.Knots.SequenceEqual(knots));

        if (index >= 0)
            return index;

        lookups.Add(new KnotLookup(column, knots));

        return lookups.Count - 1;
    }
}
