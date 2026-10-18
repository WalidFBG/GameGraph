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

        state.Score[state.TeamInPossession] += points;
        incidentCollector.Possession(new PossessionIncident(state.SimulationIndex, state.TeamInPossession, state.Period, points));

        state.RemainingInPeriod--;

        if (state.RemainingInPeriod > 0)
        {
            state.TeamInPossession = Sides.Opponent(state.TeamInPossession);
            return NbaNodes.Possession;
        }

        state.Period++;

        if (state.Period == state.Periods.Length)
            return NodeSpec<NbaState, INbaIncidentCollector>.End;

        state.TeamInPossession = state.Periods[state.Period].StartingTeamInPossession;
        state.RemainingInPeriod = state.Periods[state.Period].Possessions;
        return NbaNodes.Possession;
    }
}
