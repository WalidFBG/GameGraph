namespace GameGraph.Engine;

public sealed class EmptyDecisionCollector<TState> : IDecisionCollector<TState>
{
    public void Collect(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features, int outcome)
    {
    }
}
