namespace GameGraph.Models.Predictors;

internal sealed record GivenColumn(int Column) : IParameterValue
{
    public double Value(ReadOnlySpan<double> features) => features[Column];
}
