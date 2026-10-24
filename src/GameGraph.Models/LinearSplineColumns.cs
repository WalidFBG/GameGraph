using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal sealed class LinearSplineColumns(int column, double[] knotPositions, double[] centres, double[] scales) : ITermColumns
{
    public int Count => knotPositions.Length - 1;

    public void Expand(ReadOnlySpan<double> features, Span<double> columns)
    {
        var value = features[column];

        for (var s = 0; s < Count; s++)
        {
            var lower = knotPositions[s];
            var segment = Math.Min(Math.Max(value, lower), knotPositions[s + 1]) - lower;

            columns[s] = (segment - centres[s]) / scales[s];
        }
    }

    public static LinearSplineColumns FromFitted(FittedLinearSplineTerm term, IReadOnlyList<string> features) =>
        new(TermColumns.ColumnOf(term.Feature, features), term.KnotPositions, term.Centres, term.Scales);
}
