using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaTransitions
{
    public static Next<NbaState> JumpBall(NbaState state, int homeWon)
    {
        state.TeamInPossession = Yes(homeWon) ? Sides.Home : Sides.Away;

        if (state.Period == 0)
            state.FirstPeriodStarter = state.TeamInPossession;

        return NextPlay(state);
    }

    public static Next<NbaState> AwardedFreeThrow(NbaState state, int awarded)
    {
        if (awarded == AwardedFreeThrows.None)
        {
            var ballToDefence = state.BallAwardedToDefence;

            state.LastAwarded = AwardedFreeThrows.None;
            state.BallAwardedToDefence = false;

            return ballToDefence ? EndOfPossession(state, PlayStart.AwardedBall) : ActionOrPeriodEnd(state);
        }

        var toDefence = AwardedFreeThrows.ToDefence(awarded);

        state.LastAwarded = awarded;
        state.AwardedTeam = toDefence ? Sides.Opponent(state.TeamInPossession) : state.TeamInPossession;
        state.BallAwardedToDefence |= toDefence && AwardedFreeThrows.MovesTheBall(AwardedFreeThrows.KindOf(awarded));

        return NbaNodes.AwardedFreeThrowMade;
    }

    public static Next<NbaState> AwardedFreeThrowMade(NbaState state, int made)
    {
        if (Yes(made))
            state.Score[state.AwardedTeam]++;

        state.Incidents.AwardedFreeThrow(new AwardedFreeThrowIncident(state.AwardedTeam, state.Period, Yes(made)));
        return NbaNodes.AwardedFreeThrow;
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
            state.FreeThrowsLeft = FreeThrows.Bonus;
            return NbaNodes.FreeThrowMade;
        }

        state.ShotClock = Math.Max(state.ShotClock, Graph.ShotClock.SideOutMinimum);
        StartPlay(state, PlayStart.SideOut);
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

        state.ShotClock = Graph.ShotClock.OffensiveRebound;
        StartPlay(state, PlayStart.OffensiveRebound);
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

        StartPeriod(state);

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

    private static void StartPeriod(NbaState state)
    {
        state.PeriodClock = Periods.SecondsIn(state.Period);
        state.TeamFouls[Sides.Home] = 0;
        state.TeamFouls[Sides.Away] = 0;
        StartPossession(state, PlayStart.PeriodStart);
    }

    private static void StartPossession(NbaState state, PlayStart start)
    {
        state.PossessionPoints = 0;
        state.ShotClock = Graph.ShotClock.Full;
        StartPlay(state, start);
    }

    private static void StartPlay(NbaState state, PlayStart start)
    {
        state.Start = start;
        state.Action = Graph.ActionType.NotSet;
        state.ShotPoints = 0;
        state.Fouled = false;
        state.FreeThrowsLeft = 0;
        state.LastMiss = Miss.NotSet;
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
