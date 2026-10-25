using GameGraph.Contract.Distributions;

namespace GameGraph.Models.Distributions;

internal sealed class Choice : IDistribution
{
    private const int ParametersPerCandidate = 2;
    private const double NoSets = -1e300;

    public double LogProbability(ReadOnlySpan<double> parameters, int outcome)
    {
        var (candidates, size) = Decision(parameters);

        if (outcome < 0 || outcome >> candidates != 0 || int.PopCount(outcome) != size)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, $"A choice of {size} is a set of {size} of its {candidates} candidates' rows");

        var chosen = 0.0;

        for (var c = 0; c < candidates; c++)
        {
            if ((outcome >> c & 1) == 1)
                chosen += Utility(parameters, c);
        }

        return chosen - SuffixSets(parameters, candidates, size)[0][size];
    }

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        Uniforms.Require(uniform);

        var (candidates, size) = Decision(parameters);
        var suffixes = SuffixSets(parameters, candidates, size);
        var remaining = size;
        var outcome = 0;

        for (var c = 0; c < candidates; c++)
        {
            var include = remaining > 0 ? Math.Exp(Utility(parameters, c) + suffixes[c + 1][remaining - 1] - suffixes[c][remaining]) : 0;

            if (uniform < include)
            {
                uniform /= include;
                outcome |= 1 << c;
                remaining--;
            }
            else
            {
                uniform = (uniform - include) / (1 - include);
            }
        }

        return outcome;
    }

    public double[] Probabilities(ReadOnlySpan<double> parameters)
    {
        var (candidates, size) = Decision(parameters);
        var whole = SuffixSets(parameters, candidates, size)[0][size];
        var probabilities = new double[candidates];

        for (var c = 0; c < candidates; c++)
            probabilities[c] = Math.Exp(Utility(parameters, c) + SetsWithout(parameters, candidates, size - 1, c) - whole);

        return probabilities;
    }

    private static (int Candidates, int Size) Decision(ReadOnlySpan<double> parameters)
    {
        var candidates = parameters.Length / ParametersPerCandidate;

        if (candidates == 0 || parameters.Length != candidates * ParametersPerCandidate)
            throw new ArgumentException($"A choice needs a utility and a size for each of one or more candidates, got {parameters.Length} values", nameof(parameters));

        if (candidates > ChoiceDistribution.MostCandidates)
            throw new ArgumentException($"A choice has at most {ChoiceDistribution.MostCandidates} candidates, got {candidates}", nameof(parameters));

        var size = parameters[1];

        for (var c = 1; c < candidates; c++)
        {
            if (parameters[c * ParametersPerCandidate + 1] != size)
                throw new ArgumentException("Every candidate of a choice has the same size", nameof(parameters));
        }

        if (size != Math.Round(size) || size < 1 || size > candidates)
            throw new ArgumentException($"A choice of {size} is not a whole number from 1 to its {candidates} candidates", nameof(parameters));

        return (candidates, (int)size);
    }

    private static double Utility(ReadOnlySpan<double> parameters, int candidate) => parameters[candidate * ParametersPerCandidate];

    private static double[][] SuffixSets(ReadOnlySpan<double> parameters, int candidates, int size)
    {
        var suffixes = new double[candidates + 1][];

        suffixes[candidates] = Empty(size);

        for (var c = candidates - 1; c >= 0; c--)
            suffixes[c] = Grown(suffixes[c + 1], Utility(parameters, c));

        return suffixes;
    }

    private static double SetsWithout(ReadOnlySpan<double> parameters, int candidates, int size, int left)
    {
        var sets = Empty(size);

        for (var c = candidates - 1; c >= 0; c--)
        {
            if (c != left)
                sets = Grown(sets, Utility(parameters, c));
        }

        return sets[size];
    }

    private static double[] Empty(int size)
    {
        var sets = new double[size + 1];

        Array.Fill(sets, NoSets, 1, size);

        return sets;
    }

    private static double[] Grown(double[] after, double utility)
    {
        var grown = new double[after.Length];

        grown[0] = after[0];

        for (var j = 1; j < after.Length; j++)
            grown[j] = LogAddExp(after[j], utility + after[j - 1]);

        return grown;
    }

    private static double LogAddExp(double a, double b)
    {
        var larger = Math.Max(a, b);

        return larger + LogOnePlus.Of(Math.Exp(-Math.Abs(a - b)));
    }
}
