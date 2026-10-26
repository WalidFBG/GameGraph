namespace GameGraph.Models.Predictors;

internal interface IParameterValue
{
    double Value(ReadOnlySpan<double> features, ReadOnlySpan<double> design);
}
