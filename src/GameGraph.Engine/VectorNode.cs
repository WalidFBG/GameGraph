using GameGraph.Contract;
using GameGraph.Contract.Distributions;

namespace GameGraph.Engine;

public sealed class VectorNode<TState> : NodeSpec<TState>
{
    public VectorNode(
        string name,
        DistributionDeclaration distribution,
        IReadOnlyDictionary<string, FeatureTerm<TState>[]> terms,
        IReadOnlyDictionary<string, Feature<TState>> given,
        Transition<TState> transition)
        : this(name, Declaration(name, distribution, terms, given), FeaturesOf(name, distribution, terms, given), transition)
    {
    }

    private readonly Transition<TState> _transition;

    private VectorNode(string name, ModelDeclaration model, Feature<TState>[] features, Transition<TState> transition)
        : base(name, model, [.. features.Select(f => f.Name)])
    {
        Features = features;
        _transition = transition;
    }

    public Feature<TState>[] Features { get; }

    public override ReadOnlySpan<double> WriteFeatures(TState state, ref double[] buffer)
    {
        var features = Sized(ref buffer, Features.Length);

        for (var i = 0; i < Features.Length; i++)
            features[i] = Features[i].Compute(state);

        return features;
    }

    internal override Next<TState> Apply(TState state, int outcome) => _transition(state, outcome);

    private static ModelDeclaration Declaration(
        string name,
        DistributionDeclaration distribution,
        IReadOnlyDictionary<string, FeatureTerm<TState>[]> terms,
        IReadOnlyDictionary<string, Feature<TState>> given)
    {
        if (distribution.ChoosesAmongCandidates())
            throw new ArgumentException($"{name} chooses among candidates, so it is a choice node", nameof(distribution));

        return new ModelDeclaration(
            distribution,
            terms.ToDictionary(t => t.Key, t => t.Value.Select(f => f.Term).ToArray()),
            given.ToDictionary(g => g.Key, g => g.Value.Name));
    }

    private static Feature<TState>[] FeaturesOf(
        string name,
        DistributionDeclaration distribution,
        IReadOnlyDictionary<string, FeatureTerm<TState>[]> terms,
        IReadOnlyDictionary<string, Feature<TState>> given) =>
        Distinct(
            name,
            distribution.Predicted().SelectMany(p => terms[p.Name]).Select(t => t.Feature).Concat(distribution.Given().Select(g => given[g])),
            f => f.Name);
}
