namespace GameGraph.Engine;

public sealed class GameSpec<TState, TIncidentCollector>
{
    public GameSpec(NodeSpec<TState, TIncidentCollector>[] nodes, NodeSpec<TState, TIncidentCollector> start)
    {
        if (nodes.Select(n => n.Name).Distinct().Count() != nodes.Length)
            throw new ArgumentException("Node names must be unique", nameof(nodes));

        if (!nodes.Contains(start))
            throw new ArgumentException($"The start node {start.Name} is not one of the game's nodes", nameof(start));

        Nodes = nodes;
        Start = start;
    }

    public NodeSpec<TState, TIncidentCollector>[] Nodes { get; }

    public NodeSpec<TState, TIncidentCollector> Start { get; }
}
