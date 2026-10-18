using GameGraph.Engine;

namespace GameGraph.Simulation;

public sealed class SampledOutcomeDecider<TState, TIncidentCollector>(
    Dictionary<NodeSpec<TState, TIncidentCollector>, double[]> parameters,
    SeededRandom random) : IOutcomeDecider<TState, TIncidentCollector>
{
    public int Decide(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features) =>
        node.Model.Sample(parameters[node], node.Model.Design(features), random.NextDouble());
}
