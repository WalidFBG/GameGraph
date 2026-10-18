namespace GameGraph.Engine;

public interface IDecisionCollector<TState, TIncidentCollector>
{
    void Collect(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features, int outcome);
}
