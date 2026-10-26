namespace GameGraph.Models.Predictors;

internal sealed class DesignColumns
{
    private readonly ITermColumns[] _terms;
    private readonly int[] _offsets;

    public DesignColumns(ITermColumns[] terms)
    {
        _terms = terms;
        _offsets = new int[terms.Length];

        for (var t = 0; t < terms.Length; t++)
        {
            _offsets[t] = Count;
            Count += terms[t].Count;
        }
    }

    public int Count { get; }

    public IEnumerable<int> ColumnsOf(ITermColumns term)
    {
        var index = Array.IndexOf(_terms, term);

        return index >= 0
            ? Enumerable.Range(_offsets[index], term.Count)
            : throw new ArgumentException("The term is not one of the design's", nameof(term));
    }

    public void Expand(ReadOnlySpan<double> features, Span<double> columns)
    {
        for (var t = 0; t < _terms.Length; t++)
            _terms[t].Expand(features, columns.Slice(_offsets[t], _terms[t].Count));
    }
}
