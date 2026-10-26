using GameGraph.Engine.Nodes;
using GameGraph.Nba.Graph;
using GameGraph.Simulation.Compiled;

namespace GameGraph.Nba.Tests.Unit;

internal readonly struct CompiledOutcomes(SimulationDigest digest) : IOutcomeCollector<NbaState>
{
    public void Collect(Node<NbaState> node, int outcome) => digest.Outcome(node, outcome);
}
