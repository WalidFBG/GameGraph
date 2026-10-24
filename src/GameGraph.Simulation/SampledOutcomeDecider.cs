using GameGraph.Engine;
using GameGraph.Models;

namespace GameGraph.Simulation;

public sealed class SampledOutcomeDecider<TState>(
    Dictionary<NodeSpec<TState>, Model> models,
    SeededRandom random) : IOutcomeDecider<TState>
{
    public int Decide(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features) =>
        models[node].Sample(features, random.NextDouble());
}
