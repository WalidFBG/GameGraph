using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Walk;

public interface IWalkStep<TState>
{
    int Decide(Node<TState> node, TState state);
}
