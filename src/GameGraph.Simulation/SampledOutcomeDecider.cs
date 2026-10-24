using GameGraph.Engine;
using GameGraph.Models;

namespace GameGraph.Simulation;

public sealed class SampledOutcomeDecider<TState, TIncidentCollector>(
    Dictionary<NodeSpec<TState, TIncidentCollector>, Model> models,
    SeededRandom random) : IOutcomeDecider<TState, TIncidentCollector>
{
    public int Decide(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features) =>
        models[node].Sample(features, random.NextDouble());
}
