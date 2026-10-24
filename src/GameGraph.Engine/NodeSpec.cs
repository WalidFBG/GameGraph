using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public sealed class NodeSpec<TState>
{
    private int? _index;

    public NodeSpec(string name, Feature<TState>[] features, ModelDeclaration model, Transition<TState> transition)
    {
        model.RequireFeatures(name, [.. features.Select(f => f.Name)]);

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

    public int Index => _index ?? throw new InvalidOperationException($"{Name} is not one of a game's nodes");

    internal NextNode<TState> AsNext { get; }

    internal bool IsInGame => _index is not null;

    internal void JoinGame(int index)
    {
        if (_index is not null)
            throw new InvalidOperationException($"{Name} is already one of a game's nodes");

        _index = index;
    }
}
