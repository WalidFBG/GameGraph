using GameGraph.Engine.Nodes;

namespace GameGraph.Engine;

public sealed class GraphBuilder<TState>
{
    private readonly List<Node<TState>> _nodes = [];
    private bool _isBuilt;

    public Graph<TState> Build(Node<TState> start)
    {
        if (_isBuilt)
            throw new InvalidOperationException("The graph is already built");

        _isBuilt = true;

        return new Graph<TState>(this, [.. _nodes], start);
    }

    internal int Add(Node<TState> node)
    {
        if (_isBuilt)
            throw new InvalidOperationException($"{node.Name} joins a graph that is already built");

        _nodes.Add(node);

        return _nodes.Count - 1;
    }
}
