using GameGraph.Engine.Nodes;
using GameGraph.Engine.Walk;
using GameGraph.Nba.Graph;
using GameGraph.Simulation.Compiled;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.Unit;

internal sealed class CompiledParameterCheck(FittedGraph<NbaState> fitted, CompiledGraph<NbaState> compiled) : IDecisionCollector<NbaState>
{
    private readonly Func<NbaState, double[], int>[] _parameters = [.. fitted.Graph.Nodes.Select(n => NodeCompiler<NbaState>.Parameters(n, fitted.ModelOf(n)).Compile())];
    private readonly double[] _compiled = new double[compiled.ParameterCapacity];

    public int Decisions { get; private set; }

    public List<string> Mismatches { get; } = [];

    public void Collect(Node<NbaState> node, NbaState state, ReadOnlySpan<double> features, int outcome)
    {
        var count = _parameters[compiled.Graph.IndexOf(node)](state, _compiled);
        var plain = new double[count];

        fitted.ModelOf(node).ParameterValues(features, plain);
        Decisions++;

        for (var p = 0; p < count; p++)
        {
            if (BitConverter.DoubleToInt64Bits(plain[p]) != BitConverter.DoubleToInt64Bits(_compiled[p]))
                Mismatches.Add($"{node.Name}, decision {Decisions}, parameter {p}: plain {plain[p]:R}, compiled {_compiled[p]:R}");
        }
    }
}
