using GameGraph.Engine;
using GameGraph.Models;
using GameGraph.Models.Contract;

namespace GameGraph.Simulation;

public static class SimulationModels
{
    public static Dictionary<NodeSpec<TState>, Model> For<TState>(
        GameSpec<TState> spec,
        IReadOnlyList<FittedModel> fitted)
    {
        var byNode = fitted.ToDictionary(f => f.Node);
        var nodes = spec.Nodes.Select(n => n.Name).ToArray();

        if (!byNode.Keys.Order(StringComparer.Ordinal).SequenceEqual(nodes.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"Fitted models are for {string.Join(", ", byNode.Keys)}, the game has nodes {string.Join(", ", nodes)}", nameof(fitted));

        return spec.Nodes.ToDictionary(n => n, n => For(n, byNode[n.Name]));
    }

    private static Model For<TState>(NodeSpec<TState> node, FittedModel fitted)
    {
        if (!fitted.Model.Equals(node.Model))
            throw new ArgumentException($"The fitted model of {node.Name} was declared differently from the node's model", nameof(fitted));

        return Model.FromFitted(fitted, node.Features.Select(f => f.Name).ToArray());
    }
}
