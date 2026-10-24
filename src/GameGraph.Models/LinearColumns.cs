using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal sealed record LinearColumns(int Column, double Centre, double Scale) : ITermColumns
{
    public int Count => 1;

    public void Expand(ReadOnlySpan<double> features, Span<double> columns) => columns[0] = (features[Column] - Centre) / Scale;

    public static LinearColumns FromFit(string feature, LinearFit fit, IReadOnlyList<string> features) =>
        new(TermColumns.ColumnOf(feature, features), fit.Centre, fit.Scale);
}
