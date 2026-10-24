using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaTransitions
{
    public const int BonusFreeThrows = 2;

    public static Next<NbaState> ActionType(NbaState state, int category)
    {
        switch (ActionTypes.FromCategory(category))
        {
            case Graph.ActionType.TwoPointAttempt:
                state.ShotPoints = 2;
                return NbaNodes.FouledOnShot;
            case Graph.ActionType.ThreePointAttempt:
                state.ShotPoints = 3;
                return NbaNodes.FouledOnShot;
            case Graph.ActionType.Turnover:
                return EndOfPossession(state);
            case Graph.ActionType.NonShootingFoul:
                state.TeamFouls[Sides.Opponent(state.TeamInPossession)]++;
                return NbaNodes.InBonus;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Not an action type");
        }
    }

    public static Next<NbaState> FouledOnShot(NbaState state, int fouled)
    {
        state.Fouled = Yes(fouled);

        if (state.Fouled)
            state.TeamFouls[Sides.Opponent(state.TeamInPossession)]++;

        return NbaNodes.ShotMade;
    }

    public static Next<NbaState> ShotMade(NbaState state, int made)
    {
        if (Yes(made))
            Score(state, state.ShotPoints);

        if (state.Fouled)
        {
            state.FreeThrowsLeft = Yes(made) ? 1 : state.ShotPoints;
            return NbaNodes.FreeThrowMade;
        }

        if (Yes(made))
            return EndOfPossession(state);

        state.LastMiss = state.ShotPoints == 3 ? Miss.Three : Miss.Two;
        return NbaNodes.OffensiveRebound;
    }

    public static Next<NbaState> InBonus(NbaState state, int inBonus)
    {
        if (!Yes(inBonus))
            return NbaNodes.ActionType;

        state.FreeThrowsLeft = BonusFreeThrows;
        return NbaNodes.FreeThrowMade;
    }

    public static Next<NbaState> FreeThrowMade(NbaState state, int made)
    {
        if (Yes(made))
            Score(state, 1);

        state.FreeThrowsLeft--;

        if (state.FreeThrowsLeft > 0)
            return NbaNodes.FreeThrowMade;

        if (Yes(made))
            return EndOfPossession(state);

        state.LastMiss = Miss.FreeThrow;
        return NbaNodes.OffensiveRebound;
    }

    public static Next<NbaState> OffensiveRebound(NbaState state, int offensive)
    {
        if (!Yes(offensive))
            return EndOfPossession(state);

        state.ShotPoints = 0;
        state.Fouled = false;
        state.LastMiss = Miss.NotSet;
        return NbaNodes.ActionType;
    }

    private static Next<NbaState> EndOfPossession(NbaState state)
    {
        state.Incidents.Possession(new PossessionIncident(state.TeamInPossession, state.Period, state.PossessionPoints));

        state.PossessionPoints = 0;
        state.ShotPoints = 0;
        state.Fouled = false;
        state.LastMiss = Miss.NotSet;
        state.RemainingInPeriod--;

        if (state.RemainingInPeriod > 0)
        {
            state.TeamInPossession = Sides.Opponent(state.TeamInPossession);
            return NbaNodes.ActionType;
        }

        state.Period++;

        if (state.Period == state.Periods.Length)
            return Next<NbaState>.GameOver;

        state.TeamInPossession = state.Periods[state.Period].StartingTeamInPossession;
        state.RemainingInPeriod = state.Periods[state.Period].Possessions;
        state.TeamFouls[Sides.Home] = 0;
        state.TeamFouls[Sides.Away] = 0;
        return NbaNodes.ActionType;
    }

    private static void Score(NbaState state, int points)
    {
        state.Score[state.TeamInPossession] += points;
        state.PossessionPoints += points;
    }

    private static bool Yes(int outcome) => outcome switch
    {
        1 => true,
        0 => false,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A yes or no decision is 1 or 0"),
    };
}
