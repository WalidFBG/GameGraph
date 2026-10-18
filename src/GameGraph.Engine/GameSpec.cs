namespace GameGraph.Engine;

public sealed class GameSpec<TState>
{
    public GameSpec(NodeSpec<TState>[] nodes, NodeSpec<TState> start)
    {
        if (nodes.Contains(NodeSpec<TState>.End))
            throw new ArgumentException("End is not a node of the game", nameof(nodes));

        if (nodes.Select(n => n.Name).Distinct().Count() != nodes.Length)
            throw new ArgumentException("Node names must be unique", nameof(nodes));

        if (!nodes.Contains(start))
            throw new ArgumentException($"The start node {start.Name} is not one of the game's nodes", nameof(start));

        Nodes = nodes;
        Start = start;
    }

    public NodeSpec<TState>[] Nodes { get; }

    public NodeSpec<TState> Start { get; }
}
