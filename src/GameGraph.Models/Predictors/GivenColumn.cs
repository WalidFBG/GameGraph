namespace GameGraph.Models.Predictors;

internal sealed record GivenColumn(int Column) : IParameterValue
{
    public double Value(ReadOnlySpan<double> features, ReadOnlySpan<int> segments, ReadOnlySpan<double> located) => features[Column];
}
