namespace GameGraph.Models.Predictors;

internal interface IParameterValue
{
    double Value(ReadOnlySpan<double> features, ReadOnlySpan<int> segments, ReadOnlySpan<double> located);
}
