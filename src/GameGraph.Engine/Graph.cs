using GameGraph.Engine.Nodes;

namespace GameGraph.Engine;

public sealed class Graph<TState>
{
    private readonly GraphBuilder<TState> _builder;

    internal Graph(GraphBuilder<TState> builder, Node<TState>[] nodes, Node<TState> start)
    {
        if (nodes.Select(n => n.Name).Distinct().Count() != nodes.Length)
            throw new ArgumentException("Node names must be unique", nameof(nodes));

        _builder = builder;

        if (!Contains(start))
            throw new ArgumentException($"The start node {start.Name} is not one of the graph's nodes", nameof(start));

        Nodes = nodes;
        Start = start;
    }

    public Node<TState>[] Nodes { get; }

    public Node<TState> Start { get; }

    public bool Contains(Node<TState> node) => node.Builder == _builder;

    public int IndexOf(Node<TState> node) =>
        Contains(node) ? node.Index : throw new ArgumentException($"{node.Name} is not one of the graph's nodes", nameof(node));
}
