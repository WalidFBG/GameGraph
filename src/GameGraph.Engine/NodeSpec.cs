namespace GameGraph.Engine;

public sealed class NodeSpec<TState>(string name, Feature<TState>[] features, Transition<TState> transition)
{
    public static NodeSpec<TState> End { get; } = new("End", [], (_, _) => throw new InvalidOperationException("The end of the game has no transition"));

    public string Name { get; } = name;

    public Feature<TState>[] Features { get; } = features;

    public Transition<TState> Transition { get; } = transition;
}
