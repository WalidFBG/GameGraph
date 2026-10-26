using GameGraph.Contract.Distributions;

namespace GameGraph.Models.Distributions;

internal sealed class Choice : IDistribution
{
    private const int ParametersPerCandidate = 2;
    private const double NoSets = -1e300;
    private const string Name = "A choice";

    public double LogProbability(ReadOnlySpan<double> parameters, int outcome)
    {
        var shape = Shape(parameters);
        var (candidates, size) = (shape.Candidates, shape.Size);

        shape.RequireOutcome(Name, outcome);

        var chosen = 0.0;

        for (var c = 0; c < candidates; c++)
        {
            if (ChoiceShape.IsChosen(outcome, c))
                chosen += Utility(parameters, c);
        }

        Span<double> suffixes = stackalloc double[(candidates + 1) * (size + 1)];
        FillSuffixSets(parameters, candidates, size, suffixes);

        return chosen - suffixes[size];
    }

    public int Sample(ReadOnlySpan<double> parameters, double uniform)
    {
        Uniforms.Require(uniform);

        var shape = Shape(parameters);
        var (candidates, size) = (shape.Candidates, shape.Size);

        if (size == 1)
            return ChoiceShape.Choose(0, SampleOne(parameters, candidates, uniform));

        var width = size + 1;
        Span<double> suffixes = stackalloc double[(candidates + 1) * width];
        FillSuffixSets(parameters, candidates, size, suffixes);

        var remaining = size;
        var outcome = 0;

        for (var c = 0; c < candidates; c++)
        {
            var include = remaining > 0 ? Math.Exp(Utility(parameters, c) + suffixes[(c + 1) * width + remaining - 1] - suffixes[c * width + remaining]) : 0;

            if (uniform < include)
            {
                uniform /= include;
                outcome = ChoiceShape.Choose(outcome, c);
                remaining--;
            }
            else
            {
                uniform = (uniform - include) / (1 - include);
            }
        }

        if (remaining != 0)
            throw new InvalidOperationException($"A choice of {size} sampled {size - remaining} of its {candidates} candidates");

        return outcome;
    }

    public double[] Probabilities(ReadOnlySpan<double> parameters)
    {
        var shape = Shape(parameters);
        var (candidates, size) = (shape.Candidates, shape.Size);

        if (size == 0)
            return new double[candidates];

        Span<double> suffixes = stackalloc double[(candidates + 1) * (size + 1)];
        FillSuffixSets(parameters, candidates, size, suffixes);

        Span<double> sets = stackalloc double[size];
        Span<double> grown = stackalloc double[size];
        var probabilities = new double[candidates];

        for (var c = 0; c < candidates; c++)
            probabilities[c] = Math.Exp(Utility(parameters, c) + SetsWithout(parameters, candidates, size - 1, c, sets, grown) - suffixes[size]);

        return probabilities;
    }

    private static ChoiceShape Shape(ReadOnlySpan<double> parameters)
    {
        var candidates = parameters.Length / ParametersPerCandidate;

        if (parameters.Length != candidates * ParametersPerCandidate)
            throw new ArgumentException($"A choice needs a utility and a size for each of its candidates, got {parameters.Length} values", nameof(parameters));

        if (candidates == 0)
            return ChoiceShape.Of(Name, 0, 0);

        var size = parameters[1];

        for (var c = 1; c < candidates; c++)
        {
            if (parameters[c * ParametersPerCandidate + 1] != size)
                throw new ArgumentException("Every candidate of a choice has the same size", nameof(parameters));
        }

        return ChoiceShape.Of(Name, candidates, size);
    }

    private static double Utility(ReadOnlySpan<double> parameters, int candidate) => parameters[candidate * ParametersPerCandidate];

    private static int SampleOne(ReadOnlySpan<double> parameters, int candidates, double uniform)
    {
        var max = double.NegativeInfinity;

        for (var c = 0; c < candidates; c++)
            max = Math.Max(max, Utility(parameters, c));

        Span<double> weights = stackalloc double[candidates];
        var total = 0.0;

        for (var c = 0; c < candidates; c++)
        {
            weights[c] = Math.Exp(Utility(parameters, c) - max);
            total += weights[c];
        }

        var cumulative = 0.0;

        for (var c = 0; c < candidates - 1; c++)
        {
            cumulative += weights[c] / total;

            if (uniform < cumulative)
                return c;
        }

        return candidates - 1;
    }

    private static void FillSuffixSets(ReadOnlySpan<double> parameters, int candidates, int size, Span<double> suffixes)
    {
        var width = size + 1;

        Empty(suffixes.Slice(candidates * width, width));

        for (var c = candidates - 1; c >= 0; c--)
            Grow(suffixes.Slice((c + 1) * width, width), Utility(parameters, c), suffixes.Slice(c * width, width));
    }

    private static double SetsWithout(ReadOnlySpan<double> parameters, int candidates, int size, int left, Span<double> sets, Span<double> grown)
    {
        sets = sets[..(size + 1)];
        grown = grown[..(size + 1)];
        Empty(sets);

        for (var c = candidates - 1; c >= 0; c--)
        {
            if (c == left)
                continue;

            Grow(sets, Utility(parameters, c), grown);
            grown.CopyTo(sets);
        }

        return sets[size];
    }

    private static void Empty(Span<double> sets)
    {
        sets[0] = 0;
        sets[1..].Fill(NoSets);
    }

    private static void Grow(ReadOnlySpan<double> after, double utility, Span<double> grown)
    {
        grown[0] = after[0];

        for (var j = 1; j < after.Length; j++)
            grown[j] = LogAddExp(after[j], utility + after[j - 1]);
    }

    private static double LogAddExp(double a, double b)
    {
        var larger = Math.Max(a, b);

        return larger + LogOnePlus.Of(Math.Exp(-Math.Abs(a - b)));
    }
}
