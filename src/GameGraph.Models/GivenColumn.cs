namespace GameGraph.Models;

internal sealed record GivenColumn(int Column) : IParameterValue
{
    public double Value(ReadOnlySpan<double> features) => features[Column];
}
