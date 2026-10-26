using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Walk;

public interface IOutcomeDecider<TState>
{
    int Decide(Node<TState> node, TState state, ReadOnlySpan<double> features);
}
