using GameGraph.ModelFamilies;

namespace GameGraph.Engine;

public sealed class NodeSpec<TState, TIncidentCollector>
{
    private readonly Model? _model;

    public NodeSpec(string name, Feature<TState>[] features, IModelFamily family, ITerm[] terms, Transition<TState, TIncidentCollector> transition)
    {
        Name = name;
        Features = features;
        Transition = transition;
        _model = new Model(family, terms, features.Select(f => f.Name).ToArray());
    }

    private NodeSpec(string name)
    {
        Name = name;
        Features = [];
        Transition = (_, _, _) => throw new InvalidOperationException("The end of the game has no transition");
    }

    public static NodeSpec<TState, TIncidentCollector> End { get; } = new("End");

    public string Name { get; }

    public Feature<TState>[] Features { get; }

    public Model Model => _model ?? throw new InvalidOperationException("The end of the game has no model");

    public Transition<TState, TIncidentCollector> Transition { get; }
}
