namespace GameGraph.Models.Distributions;

internal sealed class Bernoulli : IDistribution
{
    public double LogProbability(ReadOnlySpan<double> parameters, int outcome) => outcome switch
    {
        1 => Math.Log(Probability(parameters)),
        0 => LogOnePlus.Of(-Probability(parameters)),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A Bernoulli outcome is 0 or 1"),
    };

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        Uniforms.Require(uniform);

        return uniform < Probability(parameters) ? 1 : 0;
    }

    public double[] Probabilities(ReadOnlySpan<double> parameters) => [1 - Probability(parameters), Probability(parameters)];

    private static double Probability(ReadOnlySpan<double> parameters) =>
        parameters.Length == 1 ? parameters[0] : throw new ArgumentException($"A Bernoulli needs 1 parameter, got {parameters.Length}", nameof(parameters));
}
