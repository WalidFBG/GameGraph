namespace GameGraph.Models;

internal static class Bernoulli
{
    public static readonly string[] ParameterNames = ["probability"];

    public static double LogProbability(ReadOnlySpan<double> parameters, int outcome) => outcome switch
    {
        1 => Math.Log(parameters[0]),
        0 => double.LogP1(-parameters[0]),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A Bernoulli outcome is 0 or 1"),
    };

    public static int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        if (uniform is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(uniform), uniform, "A uniform is in [0, 1)");

        return uniform < parameters[0] ? 1 : 0;
    }
}
