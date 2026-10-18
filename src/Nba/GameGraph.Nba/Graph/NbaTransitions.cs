using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaTransitions
{
    public const int PointsPerScore = 2;

    public static NodeSpec<NbaState> Possession(NbaState state, int scored)
    {
        if (scored is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(scored), scored, "A possession either scores (1) or not (0)");

        if (scored == 1)
            state.Score[state.Side] += PointsPerScore;

        state.RemainingInPeriod--;

        if (state.RemainingInPeriod > 0)
        {
            state.Side = Sides.Opponent(state.Side);
            return NbaNodes.Possession;
        }

        state.Period++;

        if (state.Period == state.Periods.Length)
            return NodeSpec<NbaState>.End;

        state.Side = state.Periods[state.Period].StartingSide;
        state.RemainingInPeriod = state.Periods[state.Period].Possessions;
        return NbaNodes.Possession;
    }
}
