using GameGraph.Engine.Nodes;
using GameGraph.Engine.Walk;
using GameGraph.Simulation.Randomness;

namespace GameGraph.Simulation;

public sealed class SampledOutcomeDecider<TState>(FittedGraph<TState> fitted, SeededRandom random) : IOutcomeDecider<TState>
{
    public int Decide(Node<TState> node, TState state, ReadOnlySpan<double> features) =>
        fitted.ModelOf(node).Sample(features, random.NextDouble());
}
