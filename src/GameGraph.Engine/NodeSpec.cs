namespace GameGraph.Engine;

public sealed class NodeSpec<TState, TIncidentCollector>(string name, Feature<TState>[] features, Transition<TState, TIncidentCollector> transition)
{
    public static NodeSpec<TState, TIncidentCollector> End { get; } = new("End", [], (_, _, _) => throw new InvalidOperationException("The end of the game has no transition"));

    public string Name { get; } = name;

    public Feature<TState>[] Features { get; } = features;

    public Transition<TState, TIncidentCollector> Transition { get; } = transition;
}
