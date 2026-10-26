using GameGraph.Engine;

namespace GameGraph.Simulation;

public struct CompiledStep<TState, TCollector>(CompiledGame<TState> game, SeededRandom random, TCollector collector) : IWalkStep<TState>
    where TCollector : struct, IOutcomeCollector<TState>
{
    private readonly double[] _parameters = new double[game.ParameterCapacity];
    private TCollector _collector = collector;

    public int Decide(NodeSpec<TState> node, TState state)
    {
        var outcome = game.Decide(node, state, _parameters, random.NextDouble());

        _collector.Collect(node, outcome);

        return outcome;
    }
}
