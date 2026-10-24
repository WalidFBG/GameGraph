namespace GameGraph.Engine;

public interface IDecisionCollector<TState>
{
    void Collect(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features, int outcome);
}
