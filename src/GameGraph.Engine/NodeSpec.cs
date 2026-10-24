using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public sealed class NodeSpec<TState, TIncidentCollector>
{
    private readonly ModelDeclaration? _model;

    public NodeSpec(string name, Feature<TState>[] features, ModelDeclaration model, Transition<TState, TIncidentCollector> transition)
    {
        model.RequireFeatures(name, [.. features.Select(f => f.Name)]);

        Name = name;
        Features = features;
        Transition = transition;
        _model = model;
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

    public ModelDeclaration Model => _model ?? throw new InvalidOperationException("The end of the game has no model");

    public Transition<TState, TIncidentCollector> Transition { get; }
}
