namespace GameGraph.Models;

internal sealed class DiscreteJohnsonSu : IDistribution
{
    public double LogProbability(ReadOnlySpan<double> parameters, int outcome)
    {
        var (location, scale, skew, tail, maximum) = Unpack(parameters);

        if (outcome < 0 || outcome > maximum)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A discrete Johnson SU outcome is a whole number from 0 to its maximum");

        double Z(double x) => skew + tail * Math.Asinh((x - location) / scale);

        return LogMassBetween(Z(outcome), Z(outcome + 1)) - LogMassBetween(Z(0), Z(maximum + 1));
    }

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        var (location, scale, skew, tail, maximum) = Unpack(parameters);

        Uniforms.Require(uniform);

        var low = skew + tail * Math.Asinh((0 - location) / scale);
        var high = skew + tail * Math.Asinh((maximum + 1 - location) / scale);
        var quantile = low > 0
            ? -Normal.InverseCdf(Normal.Cdf(-low) - uniform * (Normal.Cdf(-low) - Normal.Cdf(-high)))
            : Normal.InverseCdf(Normal.Cdf(low) + uniform * (Normal.Cdf(high) - Normal.Cdf(low)));
        var time = location + scale * Math.Sinh((quantile - skew) / tail);

        return (int)Math.Min(Math.Max(Math.Floor(time), 0), maximum);
    }

    public int Outcomes(ReadOnlySpan<double> parameters) => Unpack(parameters).Maximum + 1;

    private static (double Location, double Scale, double Skew, double Tail, int Maximum) Unpack(ReadOnlySpan<double> parameters)
    {
        if (parameters.Length != 5)
            throw new ArgumentException($"A discrete Johnson SU needs 5 parameters, got {parameters.Length}", nameof(parameters));

        var maximum = parameters[4];

        if (maximum < 0 || maximum != Math.Round(maximum))
            throw new ArgumentOutOfRangeException(nameof(parameters), maximum, "A maximum is a whole number of 0 or more");

        return (parameters[0], parameters[1], parameters[2], parameters[3], (int)maximum);
    }

    private static double LogMassBetween(double low, double high)
    {
        var above = low > 0;
        var lower = above ? -high : low;
        var upper = above ? -low : high;
        var logUpper = Normal.LogCdf(upper);

        return logUpper + LogOnePlus.Of(-Math.Exp(Normal.LogCdf(lower) - logUpper));
    }
}
