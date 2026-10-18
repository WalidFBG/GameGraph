using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class ObservedOutcomeDecider(Possession[] possessions) : IOutcomeDecider<NbaState, INbaIncidentCollector>
{
    private int _next;

    public int Decide(NodeSpec<NbaState, INbaIncidentCollector> node, NbaState state, double[] features)
    {
        if (node != NbaNodes.Possession)
            throw new ArgumentOutOfRangeException(nameof(node), node.Name, "No observed outcome for this node");

        if (_next == possessions.Length)
            throw new InvalidDataException("The rules asked for more possessions than the game had");

        var possession = possessions[_next];

        if (possession.Side != state.Side || possession.Period != state.Period)
            throw new InvalidDataException($"Possession {_next}: the data has {Sides.Name(possession.Side)} in period {possession.Period}, the rules expect {Sides.Name(state.Side)} in period {state.Period}");

        _next++;
        return possession.Scored ? 1 : 0;
    }

    public void EnsureAllUsed()
    {
        if (_next != possessions.Length)
            throw new InvalidDataException($"The game ended after {_next} of {possessions.Length} possessions");
    }
}
