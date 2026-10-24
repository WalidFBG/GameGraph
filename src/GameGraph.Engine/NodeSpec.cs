using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public sealed class NodeSpec<TState>
{
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

    internal NextNode<TState> AsNext { get; }
}
