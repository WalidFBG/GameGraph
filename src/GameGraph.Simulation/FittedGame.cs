using GameGraph.Contract;
using GameGraph.Engine;
using GameGraph.Models;

namespace GameGraph.Simulation;

public sealed class FittedGame<TState>
{
    private readonly Model[] _models;

    public FittedGame(GameSpec<TState> spec, IReadOnlyList<FittedModel> fitted)
    {
        var byNode = fitted.ToDictionary(f => f.Node);
        var nodes = spec.Nodes.Select(n => n.Name).ToArray();

        if (!byNode.Keys.Order(StringComparer.Ordinal).SequenceEqual(nodes.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"Fitted models are for {string.Join(", ", byNode.Keys)}, the game has nodes {string.Join(", ", nodes)}", nameof(fitted));

        Spec = spec;
        _models = [.. spec.Nodes.Select(n => ModelFor(n, byNode[n.Name]))];
    }

    public GameSpec<TState> Spec { get; }

    public Model ModelOf(NodeSpec<TState> node) => _models[Spec.IndexOf(node)];

    private static Model ModelFor(NodeSpec<TState> node, FittedModel fitted)
    {
        if (!fitted.Model.Equals(node.Model))
            throw new ArgumentException($"The fitted model of {node.Name} was declared differently from the node's model", nameof(fitted));

        return Model.FromFitted(fitted, node.FeatureNames);
    }
}
