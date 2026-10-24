namespace GameGraph.Models;

internal interface IParameterValue
{
    double Value(ReadOnlySpan<double> features);
}
