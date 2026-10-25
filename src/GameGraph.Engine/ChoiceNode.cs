using System.Linq.Expressions;
using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public sealed class ChoiceNode<TState> : NodeSpec<TState>
{
    private static readonly ChoiceDistribution Distribution = new();

    private readonly Func<TState, int[]> _candidates;

    public ChoiceNode(string name, Expression<Func<TState, int[]>> candidates, CandidateTerm<TState>[] terms, Transition<TState> transition)
        : this(name, candidates, terms, Distinct(name, terms.Select(t => t.Feature), f => f.Name), transition)
    {
    }

    private ChoiceNode(
        string name,
        Expression<Func<TState, int[]>> candidates,
        CandidateTerm<TState>[] terms,
        CandidateFeature<TState>[] features,
        Transition<TState> transition)
        : base(name, Declaration(terms), [.. features.Select(f => f.Name)], transition)
    {
        if (features.Length == 0)
            throw new ArgumentException($"{name} needs a feature to tell its candidates apart", nameof(terms));

        Candidates = candidates;
        Features = features;
        _candidates = candidates.Compile();
    }

    public Expression<Func<TState, int[]>> Candidates { get; }

    public CandidateFeature<TState>[] Features { get; }

    public int[] CandidatesIn(TState state) => _candidates(state);

    public override int FeatureValueCount(TState state) => CandidatesIn(state).Length * Features.Length;

    public override void WriteFeatures(TState state, Span<double> features)
    {
        var candidates = CandidatesIn(state);

        for (var c = 0; c < candidates.Length; c++)
        {
            for (var f = 0; f < Features.Length; f++)
                features[c * Features.Length + f] = Features[f].Compute(state, candidates[c]);
        }
    }

    private static ModelDeclaration Declaration(CandidateTerm<TState>[] terms) =>
        new(
            Distribution,
            Distribution.Predicted().ToDictionary(p => p.Name, _ => terms.Select(t => t.Term).ToArray()),
            new Dictionary<string, string>());
}
