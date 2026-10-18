using GameGraph.Engine;
using GameGraph.Parameters;

namespace GameGraph.Simulation;

public static class SimulationParameters
{
    public static Dictionary<NodeSpec<TState, TIncidentCollector>, double[]> For<TState, TIncidentCollector>(
        GameSpec<TState, TIncidentCollector> spec,
        IReadOnlyList<NodeParameters> parameters)
    {
        var byNode = parameters.ToDictionary(p => p.Node);
        var nodes = spec.Nodes.Select(n => n.Name).ToArray();

        if (!byNode.Keys.Order(StringComparer.Ordinal).SequenceEqual(nodes.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"Parameters are for {string.Join(", ", byNode.Keys)}, the game has nodes {string.Join(", ", nodes)}", nameof(parameters));

        return spec.Nodes.ToDictionary(n => n, n => For(n, byNode[n.Name]));
    }

    private static double[] For<TState, TIncidentCollector>(NodeSpec<TState, TIncidentCollector> node, NodeParameters parameters)
    {
        if (parameters.Family != node.Model.Family.Name)
            throw new ArgumentException($"The parameters of {node.Name} are for {parameters.Family}, the node declares {node.Model.Family.Name}", nameof(parameters));

        return node.Model.Parameters(parameters.Predictors);
    }
}
