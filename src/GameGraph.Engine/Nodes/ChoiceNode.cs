using System.Linq.Expressions;
using GameGraph.Contract.Distributions;
using GameGraph.Contract;

namespace GameGraph.Engine.Nodes;

public sealed class ChoiceNode<TState> : Node<TState>
{
    private static readonly ChoiceDistribution Distribution = new();

    private readonly Func<TState, int[]> _candidates;
    private readonly ChoiceTransition<TState> _transition;

    public ChoiceNode(
        GraphBuilder<TState> graph,
        string name,
        Expression<Func<TState, int[]>> candidates,
        Feature<TState> size,
        CandidateTerm<TState>[] terms,
        ChoiceTransition<TState> transition)
        : this(graph, name, candidates, size, terms, Distinct(name, terms.Select(t => t.Feature), f => f.Name), transition)
    {
    }

    private ChoiceNode(
        GraphBuilder<TState> graph,
        string name,
        Expression<Func<TState, int[]>> candidates,
        Feature<TState> size,
        CandidateTerm<TState>[] terms,
        CandidateFeature<TState>[] features,
        ChoiceTransition<TState> transition)
        : base(graph, name, Declaration(terms, size), NamesOf(name, features, size))
    {
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

    public Expression CandidatesInline(Expression state) => ParameterSubstitution.Inline(Candidates, state);

    public bool TryRowsOf(TState state, ReadOnlySpan<int> chosen, out int rows) => ChoiceShape.TryOutcome(CandidatesIn(state), chosen, out rows);

    public override bool HasOneOutcome(TState state) => ChoiceShape.Of(Name, CandidatesIn(state).Length, Size.Compute(state)).HasOneOutcome;

    public override ReadOnlySpan<double> WriteFeatures(TState state, ref double[] buffer)
    {
        var candidates = CandidatesIn(state);
        var size = Size.Compute(state);

        ChoiceShape.Of(Name, candidates.Length, size);

        var width = Features.Length + 1;
        var features = Sized(ref buffer, candidates.Length * width);

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
        var shape = ChoiceShape.OfOutcome(Name, candidates.Length, rows);
        Span<int> chosen = stackalloc int[shape.Size];

        shape.WriteChosen(candidates, rows, chosen);

        return _transition(state, chosen);
    }

    private static string[] NamesOf(string name, CandidateFeature<TState>[] features, Feature<TState> size)
    {
        if (features.Length == 0)
            throw new ArgumentException($"{name} needs a feature to tell its candidates apart", nameof(features));

        if (features.Any(f => f.Name == size.Name))
            throw new ArgumentException($"{name} has a candidate feature named like its size, {size.Name}", nameof(size));

        return [.. features.Select(f => f.Name), size.Name];
    }

    private static ModelDeclaration Declaration(CandidateTerm<TState>[] terms, Feature<TState> size) =>
        new(
            Distribution,
            Distribution.Predicted().ToDictionary(p => p.Name, _ => terms.Select(t => t.Term).ToArray()),
            new Dictionary<string, string> { [ChoiceDistribution.Size] = size.Name });
}

public delegate Next<TState> ChoiceTransition<TState>(TState state, ReadOnlySpan<int> chosen);
