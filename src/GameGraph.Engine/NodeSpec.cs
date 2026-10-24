using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public sealed class NodeSpec<TState, TIncidentCollector>
{
    public NodeSpec(string name, Feature<TState>[] features, ModelDeclaration model, Transition<TState, TIncidentCollector> transition)
    {
        model.RequireFeatures(name, [.. features.Select(f => f.Name)]);

        Name = name;
        Features = features;
        Model = model;
        Transition = transition;
    }

    public string Name { get; }

    public Feature<TState>[] Features { get; }

    public ModelDeclaration Model { get; }

    public Transition<TState, TIncidentCollector> Transition { get; }
}
