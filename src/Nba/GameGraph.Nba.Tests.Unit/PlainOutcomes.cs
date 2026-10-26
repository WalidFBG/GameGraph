using GameGraph.Engine.Nodes;
using GameGraph.Engine.Walk;
using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Tests.Unit;

internal sealed class PlainOutcomes(SimulationDigest digest) : IDecisionCollector<NbaState>
{
    public void Collect(Node<NbaState> node, NbaState state, ReadOnlySpan<double> features, int outcome) => digest.Outcome(node, outcome);
}
