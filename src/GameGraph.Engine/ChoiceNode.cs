using System.Linq.Expressions;
using GameGraph.Contract;
using GameGraph.Contract.Distributions;

namespace GameGraph.Engine;

public sealed class ChoiceNode<TState> : NodeSpec<TState>
{
    private static readonly ChoiceDistribution Distribution = new();

    private readonly Func<TState, int[]> _candidates;

    public ChoiceNode(
        string name,
        Expression<Func<TState, int[]>> candidates,
        Feature<TState> size,
        CandidateTerm<TState>[] terms,
        Transition<TState> transition)
        : this(name, candidates, size, terms, Distinct(name, terms.Select(t => t.Feature), f => f.Name), transition)
    {
    }

    private ChoiceNode(
        string name,
        Expression<Func<TState, int[]>> candidates,
        Feature<TState> size,
        CandidateTerm<TState>[] terms,
        CandidateFeature<TState>[] features,
        Transition<TState> transition)
        : base(name, Declaration(terms, size), [.. features.Select(f => f.Name), size.Name], transition)
    {
        if (features.Length == 0)
            throw new ArgumentException($"{name} needs a feature to tell its candidates apart", nameof(terms));

        if (features.Any(f => f.Name == size.Name))
            throw new ArgumentException($"{name} has a candidate feature named like its size, {size.Name}", nameof(size));

        Candidates = candidates;
        Size = size;
        Features = features;
        _candidates = candidates.Compile();
    }

    public Expression<Func<TState, int[]>> Candidates { get; }

    public Feature<TState> Size { get; }

    public CandidateFeature<TState>[] Features { get; }

    public int[] CandidatesIn(TState state) => _candidates(state);

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

    private static ModelDeclaration Declaration(CandidateTerm<TState>[] terms, Feature<TState> size) =>
        new(
            Distribution,
            Distribution.Predicted().ToDictionary(p => p.Name, _ => terms.Select(t => t.Term).ToArray()),
            new Dictionary<string, string> { [ChoiceDistribution.Size] = size.Name });
}
