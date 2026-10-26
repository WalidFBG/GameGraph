using GameGraph.Engine;

namespace GameGraph.Simulation;

public readonly struct NoOutcomeCollector<TState> : IOutcomeCollector<TState>
{
    public void Collect(NodeSpec<TState> node, int outcome)
    {
    }
}
