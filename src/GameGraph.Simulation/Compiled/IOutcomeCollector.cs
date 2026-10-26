using GameGraph.Engine.Nodes;

namespace GameGraph.Simulation.Compiled;

public interface IOutcomeCollector<TState>
{
    void Collect(Node<TState> node, int outcome);
}
