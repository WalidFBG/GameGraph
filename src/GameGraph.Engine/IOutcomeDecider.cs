namespace GameGraph.Engine;

public interface IOutcomeDecider<TState, TIncidentCollector>
{
    int Decide(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features);
}
