using GameGraph.Engine;
using GameGraph.ModelFamilies;

namespace GameGraph.Simulation;

public sealed class SampledOutcomeDecider<TState, TIncidentCollector>(
    Dictionary<NodeSpec<TState, TIncidentCollector>, double[]> parameters,
    SeededRandom random) : IOutcomeDecider<TState, TIncidentCollector>
{
    public int Decide(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features) =>
        random.NextDouble() < Logistic.Probability(parameters[node], features) ? 1 : 0;
}
