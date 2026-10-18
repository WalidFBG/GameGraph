using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaTransitions
{
    public const int PointsPerScore = 2;

    public static NodeSpec<NbaState, INbaIncidentCollector> Possession(NbaState state, int scored, INbaIncidentCollector incidentCollector)
    {
        if (scored is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(scored), scored, "A possession either scores (1) or not (0)");

        var points = scored * PointsPerScore;

        state.Score[state.Side] += points;
        incidentCollector.Possession(new PossessionIncident(state.Side, state.Period, points));

        state.RemainingInPeriod--;

        if (state.RemainingInPeriod > 0)
        {
            state.Side = Sides.Opponent(state.Side);
            return NbaNodes.Possession;
        }

        state.Period++;

        if (state.Period == state.Periods.Length)
            return NodeSpec<NbaState, INbaIncidentCollector>.End;

        state.Side = state.Periods[state.Period].StartingSide;
        state.RemainingInPeriod = state.Periods[state.Period].Possessions;
        return NbaNodes.Possession;
    }
}
