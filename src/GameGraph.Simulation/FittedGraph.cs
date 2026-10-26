using GameGraph.Contract;
using GameGraph.Engine.Nodes;
using GameGraph.Engine;
using GameGraph.Models;

namespace GameGraph.Simulation;

public sealed class FittedGraph<TState>
{
    private readonly Model[] _models;

    public FittedGraph(Graph<TState> graph, IReadOnlyList<FittedModel> fitted)
    {
        var byNode = fitted.ToDictionary(f => f.Node);
        var nodes = graph.Nodes.Select(n => n.Name).ToArray();

        if (!byNode.Keys.Order(StringComparer.Ordinal).SequenceEqual(nodes.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"Fitted models are for {string.Join(", ", byNode.Keys)}, the game has nodes {string.Join(", ", nodes)}", nameof(fitted));

        Graph = graph;
        _models = [.. graph.Nodes.Select(n => ModelFor(n, byNode[n.Name]))];
    }

    public Graph<TState> Graph { get; }

    public Model ModelOf(Node<TState> node) => _models[Graph.IndexOf(node)];

    private static Model ModelFor(Node<TState> node, FittedModel fitted)
    {
        if (!fitted.Model.Equals(node.Model))
            throw new ArgumentException($"The fitted model of {node.Name} was declared differently from the node's model", nameof(fitted));

        return Model.FromFitted(fitted, node.FeatureNames);
    }
}
