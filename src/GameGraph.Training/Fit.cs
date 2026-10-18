namespace GameGraph.Training;

public sealed record Fit(double[] Parameters, int BestStep, double[] TrainingNegativeLogLikelihoods, double[] ValidationNegativeLogLikelihoods)
{
    public int LastStep => ValidationNegativeLogLikelihoods.Length - 1;
}
