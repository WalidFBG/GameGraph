using GameGraph.Engine.Nodes;

namespace GameGraph.Simulation.Compiled;

public readonly struct NoOutcomeCollector<TState> : IOutcomeCollector<TState>
{
    public void Collect(Node<TState> node, int outcome)
    {
    }
}
