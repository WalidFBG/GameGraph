using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Walk;

public sealed class NoDecisionCollector<TState> : IDecisionCollector<TState>
{
    public void Collect(Node<TState> node, TState state, ReadOnlySpan<double> features, int outcome)
    {
    }
}
