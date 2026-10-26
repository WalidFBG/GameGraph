using GameGraph.Engine.Nodes;
using GameGraph.Engine.Walk;
using GameGraph.Simulation.Randomness;

namespace GameGraph.Simulation.Compiled;

public struct CompiledStep<TState, TCollector>(CompiledGraph<TState> compiled, SeededRandom random, TCollector collector) : IWalkStep<TState>
    where TCollector : struct, IOutcomeCollector<TState>
{
    private readonly double[] _parameters = new double[compiled.ParameterCapacity];
    private TCollector _collector = collector;

    public int Decide(Node<TState> node, TState state)
    {
        var outcome = compiled.Decide(node, state, _parameters, random.NextDouble());

        _collector.Collect(node, outcome);

        return outcome;
    }
}
