using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.Unit;

internal readonly struct CompiledOutcomes(SimulationDigest digest) : IOutcomeCollector<NbaState>
{
    public void Collect(NodeSpec<NbaState> node, int outcome) => digest.Outcome(node, outcome);
}
