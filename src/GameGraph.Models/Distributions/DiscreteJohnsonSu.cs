namespace GameGraph.Models.Distributions;

internal sealed class DiscreteJohnsonSu : IDistribution
{
    private const double NarrowOutcome = 1e-3;
    private const double NarrowRange = 1e-8;

    public double LogProbability(ReadOnlySpan<double> parameters, int outcome)
    {
        var (location, scale, skew, tail, maximum) = Unpack(parameters);

        if (outcome < 0 || outcome > maximum)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A discrete Johnson SU outcome is a whole number from 0 to its maximum");

        double Edge(double x) => Math.Asinh((x - location) / scale);

        return LogMass(skew, tail, Edge(outcome), Edge(outcome + 1)) - LogMass(skew, tail, Edge(0), Edge(maximum + 1));
    }

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        var (location, scale, skew, tail, maximum) = Unpack(parameters);

        Uniforms.Require(uniform);

        var start = Math.Asinh((0 - location) / scale);
        var end = Math.Asinh((maximum + 1 - location) / scale);
        var low = skew + tail * start;
        var high = skew + tail * end;
        var width = tail * (end - start);

        if (IsNarrow(width, low + width / 2, NarrowRange))
            return Floored(location + scale * Math.Sinh(start + uniform * (end - start)), maximum);

        double quantile;

        if (low > 0)
        {
            var above = Normal.Cdf(-low);
            quantile = -Normal.InverseCdf(above - uniform * (above - Normal.Cdf(-high)));
        }
        else
        {
            var below = Normal.Cdf(low);
            quantile = Normal.InverseCdf(below + uniform * (Normal.Cdf(high) - below));
        }

        return Floored(location + scale * Math.Sinh((quantile - skew) / tail), maximum);
    }

    public double[] Probabilities(ReadOnlySpan<double> parameters)
    {
        var (location, scale, skew, tail, maximum) = Unpack(parameters);

        double Edge(double x) => Math.Asinh((x - location) / scale);

        var logTotal = LogMass(skew, tail, Edge(0), Edge(maximum + 1));
        var probabilities = new double[maximum + 1];

        for (var outcome = 0; outcome <= maximum; outcome++)
            probabilities[outcome] = Math.Exp(LogMass(skew, tail, Edge(outcome), Edge(outcome + 1)) - logTotal);

        return probabilities;
    }

    private static (double Location, double Scale, double Skew, double Tail, int Maximum) Unpack(ReadOnlySpan<double> parameters)
    {
        if (parameters.Length != 5)
            throw new ArgumentException($"A discrete Johnson SU needs 5 parameters, got {parameters.Length}", nameof(parameters));

        var maximum = parameters[4];

        if (maximum < 0 || maximum != Math.Round(maximum))
            throw new ArgumentOutOfRangeException(nameof(parameters), maximum, "A maximum is a whole number of 0 or more");

        return (parameters[0], parameters[1], parameters[2], parameters[3], (int)maximum);
    }

    private static double LogMass(double skew, double tail, double start, double end)
    {
        var low = skew + tail * start;
        var width = tail * (end - start);
        var middle = low + width / 2;

        return IsNarrow(width, middle, NarrowOutcome)
            ? Normal.LogDensity(middle) + Math.Log(width) + LogOnePlus.Of((middle * middle - 1) * width * width / 24)
            : LogMassBetween(low, skew + tail * end);
    }

    private static bool IsNarrow(double width, double middle, double threshold) => width * Math.Max(1, Math.Abs(middle)) < threshold;

    private static int Floored(double time, int maximum) => (int)Math.Min(Math.Max(Math.Floor(time), 0), maximum);

    private static double LogMassBetween(double low, double high)
    {
        var above = low > 0;
        var lower = above ? -high : low;
        var upper = above ? -low : high;
        var logUpper = Normal.LogCdf(upper);

        return logUpper + LogOnePlus.Of(-Math.Exp(Normal.LogCdf(lower) - logUpper));
    }
}
