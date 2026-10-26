using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Walk;

public interface IDecisionCollector<TState>
{
    void Collect(Node<TState> node, TState state, ReadOnlySpan<double> features, int outcome);
}
