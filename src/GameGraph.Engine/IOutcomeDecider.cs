namespace GameGraph.Engine;

public interface IOutcomeDecider<TState>
{
    int Decide(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features);
}
