namespace GameGraph.Models;

internal sealed class Categorical(int categories) : IDistribution
{
    public double LogProbability(ReadOnlySpan<double> parameters, int outcome)
    {
        if (outcome < 0 || outcome >= categories)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, $"A categorical outcome is a category from 0 to {categories - 1}");

        var max = Max(parameters);
        var total = Math.Exp(-max);

        foreach (var logOdds in parameters)
            total += Math.Exp(logOdds - max);

        return Score(parameters, outcome) - max - Math.Log(total);
    }

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        Uniforms.Require(uniform);

        var max = Max(parameters);
        var total = Math.Exp(-max);

        foreach (var logOdds in parameters)
            total += Math.Exp(logOdds - max);

        var cumulative = 0.0;

        for (var category = 0; category < categories - 1; category++)
        {
            cumulative += Math.Exp(Score(parameters, category) - max) / total;

            if (uniform < cumulative)
                return category;
        }

        return categories - 1;
    }

    private static double Score(ReadOnlySpan<double> parameters, int category) => category == 0 ? 0 : parameters[category - 1];

    private double Max(ReadOnlySpan<double> parameters)
    {
        if (parameters.Length != categories - 1)
            throw new ArgumentException($"{categories} categories need {categories - 1} log-odds, got {parameters.Length}", nameof(parameters));

        var max = 0.0;

        foreach (var logOdds in parameters)
            max = Math.Max(max, logOdds);

        return max;
    }
}
