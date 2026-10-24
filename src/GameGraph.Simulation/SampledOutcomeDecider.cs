using GameGraph.Engine;

namespace GameGraph.Simulation;

public sealed class SampledOutcomeDecider<TState>(FittedGame<TState> game, SeededRandom random) : IOutcomeDecider<TState>
{
    public int Decide(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features) =>
        game.ModelOf(node).Sample(features, random.NextDouble());
}
