using GameGraph.Contract.Terms;

namespace GameGraph.Models.Predictors;

internal sealed class LinearSplineColumns : ITermColumns, IEquatable<LinearSplineColumns>
{
    private readonly int _column;
    private readonly double[] _knotPositions;
    private readonly double[] _centres;
    private readonly double[] _scales;

    public LinearSplineColumns(int column, double[] knotPositions, double[] centres, double[] scales)
    {
        _column = column;
        _knotPositions = knotPositions;
        _centres = centres;
        _scales = scales;
    }

    public int Count => _knotPositions.Length - 1;

    public void Expand(ReadOnlySpan<double> features, Span<double> columns)
    {
        var value = features[_column];

        for (var s = 0; s < Count; s++)
        {
            var lower = _knotPositions[s];
            var segment = Math.Min(Math.Max(value, lower), _knotPositions[s + 1]) - lower;

            columns[s] = (segment - _centres[s]) / _scales[s];
        }
    }

    public bool Equals(LinearSplineColumns? other) =>
        other is not null
        && other._column == _column
        && other._knotPositions.SequenceEqual(_knotPositions)
        && other._centres.SequenceEqual(_centres)
        && other._scales.SequenceEqual(_scales);

    public override bool Equals(object? obj) => Equals(obj as LinearSplineColumns);

    public override int GetHashCode() => HashCode.Combine(_column, Count);

    public static LinearSplineColumns FromFit(string feature, LinearSplineFit fit, IReadOnlyList<string> features) =>
        new(TermColumns.ColumnOf(feature, features), fit.KnotPositions, fit.Centres, fit.Scales);
}
