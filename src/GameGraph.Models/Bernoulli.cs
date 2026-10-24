namespace GameGraph.Models;

internal sealed class Bernoulli : IDistribution
{
    public double LogProbability(ReadOnlySpan<double> parameters, int outcome) => outcome switch
    {
        1 => Math.Log(parameters[0]),
        0 => LogOnePlus.Of(-parameters[0]),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A Bernoulli outcome is 0 or 1"),
    };

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        Uniforms.Require(uniform);

        return uniform < parameters[0] ? 1 : 0;
    }
}
