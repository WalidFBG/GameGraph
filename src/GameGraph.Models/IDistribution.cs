namespace GameGraph.Models;

internal interface IDistribution
{
    double LogProbability(ReadOnlySpan<double> parameters, int outcome);

    int Sample(ReadOnlySpan<double> parameters, double uniform);

    double[] Probabilities(ReadOnlySpan<double> parameters);
}
