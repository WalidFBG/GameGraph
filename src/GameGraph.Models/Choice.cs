namespace GameGraph.Models;

internal sealed class Choice : IDistribution
{
    public double LogProbability(ReadOnlySpan<double> parameters, int outcome)
    {
        if (outcome < 0 || outcome >= parameters.Length)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, $"A choice is a candidate from 0 to {parameters.Length - 1}");

        var max = Max(parameters);
        var total = 0.0;

        foreach (var utility in parameters)
            total += Math.Exp(utility - max);

        return parameters[outcome] - max - Math.Log(total);
    }

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        Uniforms.Require(uniform);

        var max = Max(parameters);
        var total = 0.0;

        foreach (var utility in parameters)
            total += Math.Exp(utility - max);

        var cumulative = 0.0;

        for (var candidate = 0; candidate < parameters.Length - 1; candidate++)
        {
            cumulative += Math.Exp(parameters[candidate] - max) / total;

            if (uniform < cumulative)
                return candidate;
        }

        return parameters.Length - 1;
    }

    public double[] Probabilities(ReadOnlySpan<double> parameters)
    {
        var max = Max(parameters);
        var probabilities = new double[parameters.Length];
        var total = 0.0;

        for (var candidate = 0; candidate < parameters.Length; candidate++)
        {
            probabilities[candidate] = Math.Exp(parameters[candidate] - max);
            total += probabilities[candidate];
        }

        for (var candidate = 0; candidate < parameters.Length; candidate++)
            probabilities[candidate] /= total;

        return probabilities;
    }

    private static double Max(ReadOnlySpan<double> parameters)
    {
        if (parameters.Length == 0)
            throw new ArgumentException("A choice needs at least one candidate", nameof(parameters));

        var max = double.NegativeInfinity;

        foreach (var utility in parameters)
            max = Math.Max(max, utility);

        return max;
    }
}
