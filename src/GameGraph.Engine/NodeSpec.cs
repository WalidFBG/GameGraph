using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public sealed class NodeSpec<TState>
{
    private int? _index;

    public NodeSpec(
        string name,
        DistributionDeclaration distribution,
        IReadOnlyDictionary<string, FeatureTerm<TState>[]> terms,
        IReadOnlyDictionary<string, Feature<TState>> given,
        Transition<TState> transition)
    {
        var model = new ModelDeclaration(
            distribution,
            terms.ToDictionary(t => t.Key, t => t.Value.Select(f => f.Term).ToArray()),
            given.ToDictionary(g => g.Key, g => g.Value.Name));
        Feature<TState>[] features =
        [
            .. distribution.Predicted().SelectMany(p => terms[p.Name]).Select(t => t.Feature)
                .Concat(distribution.Given().Select(g => given[g]))
                .Distinct(),
        ];
        var shared = features.GroupBy(f => f.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

        if (shared.Length > 0)
            throw new ArgumentException($"{name} has different features named {string.Join(", ", shared)}", nameof(terms));

        Name = name;
        Features = features;
        Model = model;
        Transition = transition;
        AsNext = new(this);
    }

    public string Name { get; }

    public Feature<TState>[] Features { get; }

    public ModelDeclaration Model { get; }

    public Transition<TState> Transition { get; }

    internal NextNode<TState> AsNext { get; }

    internal bool IsInGame => _index is not null;

    internal int Index => _index ?? throw new InvalidOperationException($"{Name} is not one of a game's nodes");

    internal void JoinGame(int index) => _index = index;
}
