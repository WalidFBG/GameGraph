namespace GameGraph.Models.Predictors;

internal interface ITermColumns
{
    int Count { get; }

    void Expand(ReadOnlySpan<double> features, Span<double> columns);
}
