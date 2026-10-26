using GameGraph.Engine;
using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Tests.Unit;

internal sealed class PlainOutcomes(SimulationDigest digest) : IDecisionCollector<NbaState>
{
    public void Collect(NodeSpec<NbaState> node, NbaState state, ReadOnlySpan<double> features, int outcome) => digest.Outcome(node, outcome);
}
