using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.Unit;

internal sealed class CompiledParameterCheck(FittedGame<NbaState> fitted, CompiledGame<NbaState> compiled) : IDecisionCollector<NbaState>
{
    private readonly Func<NbaState, double[], int>[] _parameters = [.. compiled.Spec.Nodes.Select(compiled.ParametersOf)];
    private readonly double[] _compiled = new double[compiled.ParameterCapacity];

    public int Decisions { get; private set; }

    public List<string> Mismatches { get; } = [];

    public void Collect(NodeSpec<NbaState> node, NbaState state, ReadOnlySpan<double> features, int outcome)
    {
        var count = _parameters[compiled.Spec.IndexOf(node)](state, _compiled);
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
