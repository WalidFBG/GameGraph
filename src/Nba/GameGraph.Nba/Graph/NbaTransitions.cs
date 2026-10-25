using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaTransitions
{
    public const int BonusFreeThrows = 2;

    public static Next<NbaState> JumpBall(NbaState state, int homeWon)
    {
        state.TeamInPossession = Yes(homeWon) ? Sides.Home : Sides.Away;

        if (state.Period == 0)
            state.FirstPeriodStarter = state.TeamInPossession;

        return NextPlay(state);
    }

    public static Next<NbaState> AwardedFreeThrow(NbaState state, int awarded)
    {
        switch (awarded)
        {
            case AwardedFreeThrows.None:
                return state.DefenceFreeThrows > 0 ? NbaNodes.AwardedBall : ActionOrPeriodEnd(state);
            case AwardedFreeThrows.Offence:
                state.AwardedTeam = state.TeamInPossession;
                return NbaNodes.AwardedFreeThrowMade;
            case AwardedFreeThrows.Defence:
                state.AwardedTeam = Sides.Opponent(state.TeamInPossession);
                state.DefenceFreeThrows++;
                return NbaNodes.AwardedFreeThrowMade;
            default:
                throw new ArgumentOutOfRangeException(nameof(awarded), awarded, "Not an awarded free throw");
        }
    }

    public static Next<NbaState> AwardedFreeThrowMade(NbaState state, int made)
    {
        if (Yes(made))
            state.Score[state.AwardedTeam]++;

        state.Incidents.AwardedFreeThrow(new AwardedFreeThrowIncident(state.AwardedTeam, state.Period, Yes(made)));
        return NbaNodes.AwardedFreeThrow;
    }

    public static Next<NbaState> AwardedBall(NbaState state, int toDefence)
    {
        state.DefenceFreeThrows = 0;

        return Yes(toDefence) ? EndOfPossession(state, PlayStart.AwardedBall) : ActionOrPeriodEnd(state);
    }

    public static Next<NbaState> PeriodRunsOut(NbaState state, int runsOut)
    {
        if (Yes(runsOut))
            return EndOfPeriod(state);

        return state.LastMiss == Miss.NotSet ? NbaNodes.ActionType : NbaNodes.OffensiveRebound;
    }

    public static Next<NbaState> ActionType(NbaState state, int category)
    {
        state.Action = ActionTypes.FromCategory(category);
        return NbaNodes.ActionTime;
    }

    public static Next<NbaState> ActionTime(NbaState state, int seconds)
    {
        if (seconds < 0 || seconds > state.Bound)
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, $"An action takes from 0 to {state.Bound} seconds here");

        state.PeriodClock -= seconds;
        state.ShotClock -= seconds;

        switch (state.Action)
        {
            case Graph.ActionType.TwoPointAttempt:
                state.ShotPoints = 2;
                return NbaNodes.FouledOnShot;
            case Graph.ActionType.ThreePointAttempt:
                state.ShotPoints = 3;
                return NbaNodes.FouledOnShot;
            case Graph.ActionType.Turnover:
                return EndOfPossession(state, PlayStart.Turnover);
            case Graph.ActionType.NonShootingFoul:
                state.TeamFouls[Sides.Opponent(state.TeamInPossession)]++;
                return NbaNodes.InBonus;
            default:
                throw new InvalidOperationException($"No action to time: {state.Action}");
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
            return EndOfPossession(state, PlayStart.MadeShot);

        state.LastMiss = state.ShotPoints == 3 ? Miss.Three : Miss.Two;
        return AfterMiss(state);
    }

    public static Next<NbaState> InBonus(NbaState state, int inBonus)
    {
        if (Yes(inBonus))
        {
            state.FreeThrowsLeft = BonusFreeThrows;
            return NbaNodes.FreeThrowMade;
        }

        state.ShotClock = Math.Max(state.ShotClock, Graph.ShotClock.SideOutMinimum);
        state.Start = PlayStart.SideOut;
        return NextPlay(state);
    }

    public static Next<NbaState> FreeThrowMade(NbaState state, int made)
    {
        if (Yes(made))
            Score(state, 1);

        state.FreeThrowsLeft--;

        if (state.FreeThrowsLeft > 0)
            return NbaNodes.FreeThrowMade;

        if (Yes(made))
            return EndOfPossession(state, PlayStart.MadeFreeThrow);

        state.LastMiss = Miss.FreeThrow;
        return AfterMiss(state);
    }

    public static Next<NbaState> OffensiveRebound(NbaState state, int offensive)
    {
        if (!Yes(offensive))
            return EndOfPossession(state, PlayStart.DefensiveRebound);

        state.ShotPoints = 0;
        state.Fouled = false;
        state.LastMiss = Miss.NotSet;
        state.ShotClock = Graph.ShotClock.OffensiveRebound;
        state.Start = PlayStart.OffensiveRebound;
        return NextPlay(state);
    }

    private static Next<NbaState> NextPlay(NbaState state) => NbaNodes.AwardedFreeThrow;

    private static Next<NbaState> ActionOrPeriodEnd(NbaState state) => state.PeriodClockBinds ? NbaNodes.PeriodRunsOut : NbaNodes.ActionType;

    private static Next<NbaState> AfterMiss(NbaState state) =>
        state.PeriodClock == 0 ? NextPlay(state) : NbaNodes.OffensiveRebound;

    private static Next<NbaState> EndOfPossession(NbaState state, PlayStart next)
    {
        ReportPossession(state);
        state.TeamInPossession = Sides.Opponent(state.TeamInPossession);
        StartPossession(state, next);
        return NextPlay(state);
    }

    private static Next<NbaState> EndOfPeriod(NbaState state)
    {
        ReportPossession(state);
        state.Period++;

        if (state.Period >= Periods.Regulation && state.Score[Sides.Home] != state.Score[Sides.Away])
            return Next<NbaState>.GameOver;

        state.PeriodClock = Periods.SecondsIn(state.Period);
        state.TeamFouls[Sides.Home] = 0;
        state.TeamFouls[Sides.Away] = 0;
        StartPossession(state, PlayStart.PeriodStart);

        if (state.Period >= Periods.Regulation)
        {
            state.TeamInPossession = Sides.Home;
            return NbaNodes.JumpBall;
        }

        state.TeamInPossession = state.Period == Periods.Regulation - 1 ? state.FirstPeriodStarter : Sides.Opponent(state.FirstPeriodStarter);
        return NextPlay(state);
    }

    private static void ReportPossession(NbaState state) =>
        state.Incidents.Possession(new PossessionIncident(state.TeamInPossession, state.Period, state.PossessionPoints));

    private static void StartPossession(NbaState state, PlayStart start)
    {
        state.PossessionPoints = 0;
        state.ShotPoints = 0;
        state.Fouled = false;
        state.LastMiss = Miss.NotSet;
        state.ShotClock = Graph.ShotClock.Full;
        state.Start = start;
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
