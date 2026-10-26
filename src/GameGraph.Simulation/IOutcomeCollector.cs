using GameGraph.Engine;

namespace GameGraph.Simulation;

public interface IOutcomeCollector<TState>
{
    void Collect(NodeSpec<TState> node, int outcome);
}
