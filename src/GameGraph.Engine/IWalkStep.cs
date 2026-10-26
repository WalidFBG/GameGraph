namespace GameGraph.Engine;

public interface IWalkStep<TState>
{
    int Decide(NodeSpec<TState> node, TState state);
}
