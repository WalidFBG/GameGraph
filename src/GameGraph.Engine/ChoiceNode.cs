using System.Linq.Expressions;
using GameGraph.Contract;
using GameGraph.Contract.Distributions;

namespace GameGraph.Engine;

public sealed class ChoiceNode<TState> : NodeSpec<TState>
{
    private static readonly ChoiceDistribution Distribution = new();

    private readonly Func<TState, int[]> _candidates;
    private readonly ChoiceTransition<TState> _transition;

    public ChoiceNode(
        string name,
        Expression<Func<TState, int[]>> candidates,
        Feature<TState> size,
        CandidateTerm<TState>[] terms,
        ChoiceTransition<TState> transition)
        : this(name, candidates, size, terms, Distinct(name, terms.Select(t => t.Feature), f => f.Name), transition)
    {
    }

    private ChoiceNode(
        string name,
        Expression<Func<TState, int[]>> candidates,
        Feature<TState> size,
        CandidateTerm<TState>[] terms,
        CandidateFeature<TState>[] features,
        ChoiceTransition<TState> transition)
        : base(name, Declaration(terms, size), [.. features.Select(f => f.Name), size.Name])
    {
        if (features.Length == 0)
            throw new ArgumentException($"{name} needs a feature to tell its candidates apart", nameof(terms));

        if (features.Any(f => f.Name == size.Name))
            throw new ArgumentException($"{name} has a candidate feature named like its size, {size.Name}", nameof(size));

        Candidates = candidates;
        Size = size;
        Features = features;
        _candidates = candidates.Compile();
        _transition = transition;
    }

    public Expression<Func<TState, int[]>> Candidates { get; }

    public Feature<TState> Size { get; }

    public CandidateFeature<TState>[] Features { get; }

    public int[] CandidatesIn(TState state) => _candidates(state);

    public bool TryRowsOf(TState state, ReadOnlySpan<int> chosen, out int rows)
    {
        var candidates = CandidatesIn(state);

        rows = 0;

        foreach (var candidate in chosen)
        {
            var row = Array.IndexOf(candidates, candidate);

            if (row < 0 || (rows >> row & 1) == 1)
                return false;

            rows |= 1 << row;
        }

        return true;
    }

    public override bool HasOneOutcome(TState state)
    {
        var size = Size.Compute(state);

        return size == 0 || size == CandidatesIn(state).Length;
    }

    public override ReadOnlySpan<double> WriteFeatures(TState state, ref double[] buffer)
    {
        var candidates = CandidatesIn(state);
        var width = Features.Length + 1;
        var features = Sized(ref buffer, candidates.Length * width);
        var size = Size.Compute(state);

        for (var c = 0; c < candidates.Length; c++)
        {
            for (var f = 0; f < Features.Length; f++)
                features[c * width + f] = Features[f].Compute(state, candidates[c]);

            features[c * width + Features.Length] = size;
        }

        return features;
    }

    internal override Next<TState> Apply(TState state, int rows)
    {
        var candidates = CandidatesIn(state);
        Span<int> chosen = stackalloc int[int.PopCount(rows)];
        var taken = 0;

        for (var row = 0; row < candidates.Length; row++)
        {
            if ((rows >> row & 1) == 1)
                chosen[taken++] = candidates[row];
        }

        if (taken != chosen.Length)
            throw new ArgumentOutOfRangeException(nameof(rows), rows, $"{Name} has {candidates.Length} candidates, so its outcome is a set of their rows");

        return _transition(state, chosen);
    }

    private static ModelDeclaration Declaration(CandidateTerm<TState>[] terms, Feature<TState> size) =>
        new(
            Distribution,
            Distribution.Predicted().ToDictionary(p => p.Name, _ => terms.Select(t => t.Term).ToArray()),
            new Dictionary<string, string> { [ChoiceDistribution.Size] = size.Name });
}
