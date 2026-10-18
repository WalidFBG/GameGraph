using GameGraph.Engine;
using GameGraph.Parameters;

namespace GameGraph.Simulation;

public static class ParameterAlignment
{
    public static Dictionary<NodeSpec<TState, TIncidentCollector>, double[]> Align<TState, TIncidentCollector>(
        GameSpec<TState, TIncidentCollector> spec,
        IReadOnlyList<NodeParameters> parameters)
    {
        var byNode = parameters.ToDictionary(p => p.Node);
        var nodes = spec.Nodes.Select(n => n.Name).ToArray();

        if (!byNode.Keys.Order(StringComparer.Ordinal).SequenceEqual(nodes.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"Parameters are for {string.Join(", ", byNode.Keys)}, the game has nodes {string.Join(", ", nodes)}", nameof(parameters));

        return spec.Nodes.ToDictionary(n => n, n => Align(n, byNode[n.Name]));
    }

    private static double[] Align<TState, TIncidentCollector>(NodeSpec<TState, TIncidentCollector> node, NodeParameters parameters)
    {
        var features = node.Features.Select(f => f.Name).ToArray();

        if (!parameters.Weights.Keys.Order(StringComparer.Ordinal).SequenceEqual(features.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"The parameters of {node.Name} have weights for {string.Join(", ", parameters.Weights.Keys)}, the node has features {string.Join(", ", features)}", nameof(parameters));

        return [parameters.Intercept, .. features.Select(f => parameters.Weights[f])];
    }
}
