using GameGraph.Engine.Nodes;
using GameGraph.Engine;

namespace GameGraph.Simulation.Compiled;

public sealed class CompiledGraph<TState>
{
    private readonly Func<TState, double[], double, int>[] _nodes;

    public CompiledGraph(FittedGraph<TState> fitted)
    {
        Graph = fitted.Graph;
        _nodes = [.. Graph.Nodes.Select(n => NodeCompiler<TState>.Decision(n, fitted.ModelOf(n)).Compile())];
        ParameterCapacity = Graph.Nodes.Max(n => NodeCompiler<TState>.Capacity(n, fitted.ModelOf(n)));
    }

    public Graph<TState> Graph { get; }

    public int ParameterCapacity { get; }

    public int Decide(Node<TState> node, TState state, double[] parameters, double uniform) => _nodes[Graph.IndexOf(node)](state, parameters, uniform);
}
