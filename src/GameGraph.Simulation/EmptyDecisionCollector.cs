using GameGraph.Engine;

namespace GameGraph.Simulation;

public sealed class EmptyDecisionCollector<TState, TIncidentCollector> : IDecisionCollector<TState, TIncidentCollector>
{
    public void Collect(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features, int outcome)
    {
    }
}
