namespace GameGraph.Models.Predictors;

internal sealed class KnotLookups
{
    private readonly List<(int Column, double[] Knots)> _lookups = [];

    public int Count => _lookups.Count;

    public int Of(int column, double[] knots)
    {
        var index = _lookups.FindIndex(l => l.Column == column && l.Knots.SequenceEqual(knots));

        if (index >= 0)
            return index;

        _lookups.Add((column, knots));

        return _lookups.Count - 1;
    }

    public void Locate(ReadOnlySpan<double> features, Span<int> segments, Span<double> values)
    {
        for (var l = 0; l < _lookups.Count; l++)
        {
            var (column, knots) = _lookups[l];
            var value = Math.Min(Math.Max(features[column], knots[0]), knots[^1]);
            var segment = 0;

            while (segment < knots.Length - 2 && value >= knots[segment + 1])
                segment++;

            segments[l] = segment;
            values[l] = value;
        }
    }
}
