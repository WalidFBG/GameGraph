using GameGraph.Engine;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public static class NbaTransitions
{
    public static Next<NbaState> Starters(NbaState state, ReadOnlySpan<int> starters)
    {
        var team = state.LineupTeam;
        var bench = 0;

        starters.CopyTo(state.OnCourt[team]);

        foreach (var player in state.Active[team])
        {
            var starts = starters.Contains(player);

            state.Started[player] = starts;
            state.IsOnCourt[player] = starts;

            if (!starts)
                state.Bench[team][bench++] = player;
        }

        if (team != Sides.Home)
            return NbaNodes.JumpBall;

        state.LineupTeam = Sides.Away;
        return NbaNodes.Starters;
    }

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

    public static Next<NbaState> OffenceTimeout(NbaState state, int called) =>
        Yes(called) ? CallTimeout(state, state.TeamInPossession) : DefenceTimeoutOrOn(state);

    public static Next<NbaState> DefenceTimeout(NbaState state, int called) =>
        Yes(called) ? CallTimeout(state, Sides.Opponent(state.TeamInPossession)) : AfterTimeouts(state);

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
                return NbaNodes.Shooter;
            case Graph.ActionType.ThreePointAttempt:
                state.ShotPoints = 3;
                return NbaNodes.Shooter;
            case Graph.ActionType.Turnover:
                return NbaNodes.TurnoverKind;
            case Graph.ActionType.NonShootingFoul:
                DefenceFouls(state);
                return NbaNodes.Fouler;
            default:
                throw new InvalidOperationException($"No action to time: {state.Action}");
        }
    }

    public static Next<NbaState> TurnoverKind(NbaState state, int kind)
    {
        if (kind is < TurnoverKinds.Other or > TurnoverKinds.OffensiveFoul)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A turnover is another kind, a team's or an offensive foul");

        state.TurnoverKind = kind;
        return kind == TurnoverKinds.Team ? EndOfPossession(state, PlayStart.DeadBallTurnover) : NbaNodes.TurnedOverBy;
    }

    public static Next<NbaState> TurnedOverBy(NbaState state, ReadOnlySpan<int> player)
    {
        state.Turnovers[player[0]]++;

        if (state.TurnoverKind != TurnoverKinds.OffensiveFoul)
            return NbaNodes.Stolen;

        state.PersonalFouls[player[0]]++;
        return EndOfPossession(state, PlayStart.DeadBallTurnover);
    }

    public static Next<NbaState> Stolen(NbaState state, int stolen) =>
        EndOfPossession(state, Yes(stolen) ? PlayStart.Steal : PlayStart.DeadBallTurnover);

    public static Next<NbaState> Shooter(NbaState state, ReadOnlySpan<int> shooter)
    {
        state.Shooter = shooter[0];
        return NbaNodes.FouledOnShot;
    }

    public static Next<NbaState> FouledOnShot(NbaState state, int fouled)
    {
        state.Fouled = Yes(fouled);

        if (!state.Fouled)
            return NbaNodes.ShotMade;

        DefenceFouls(state);
        return NbaNodes.Fouler;
    }

    public static Next<NbaState> Fouler(NbaState state, ReadOnlySpan<int> fouler)
    {
        state.PersonalFouls[fouler[0]]++;
        return state.Fouled ? NbaNodes.ShotMade : NbaNodes.FouledPlayer;
    }

    public static Next<NbaState> FouledPlayer(NbaState state, ReadOnlySpan<int> fouled)
    {
        state.Shooter = fouled[0];
        return NbaNodes.InBonus;
    }

    public static Next<NbaState> ShotMade(NbaState state, int made)
    {
        if (Yes(made))
            Score(state, state.ShotPoints);

        if (state.Fouled)
        {
            state.FreeThrowsLeft = Yes(made) ? 1 : state.ShotPoints;
            return AskTimeouts(state, Moment.FreeThrow);
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
            return AskTimeouts(state, Moment.FreeThrow);
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
        {
            state.BetweenFreeThrows = true;
            return AskTimeouts(state, Moment.FreeThrow);
        }

        if (Yes(made))
            return EndOfPossession(state, PlayStart.MadeFreeThrow);

        state.LastMiss = Miss.FreeThrow;
        return AfterMiss(state);
    }

    public static Next<NbaState> OffensiveRebound(NbaState state, int offensive)
    {
        state.ReboundToOffence = Yes(offensive);
        return NbaNodes.TeamRebound;
    }

    public static Next<NbaState> TeamRebound(NbaState state, int byTeam) =>
        Yes(byTeam) ? AfterRebound(state, byTeam: true) : NbaNodes.Rebounder;

    public static Next<NbaState> Rebounder(NbaState state, ReadOnlySpan<int> rebounder)
    {
        state.Rebounds[rebounder[0]]++;
        return AfterRebound(state, byTeam: false);
    }

    public static Next<NbaState> Substitutions(NbaState state, int teams)
    {
        if (teams is < SubstitutingTeams.None or > SubstitutingTeams.Both)
            throw new ArgumentOutOfRangeException(nameof(teams), teams, "Substitutions name no team, the offence, the defence or both");

        state.SubstitutingTeams = teams | SubstitutingTeams.Of(state.MustChange(state.TeamInPossession), state.MustChange(Sides.Opponent(state.TeamInPossession)));
        return NextChange(state);
    }

    public static Next<NbaState> SubstitutionCount(NbaState state, ReadOnlySpan<int> count)
    {
        state.ChangeSize = count[0];
        return NbaNodes.SubstitutedOff;
    }

    public static Next<NbaState> SubstitutedOff(NbaState state, ReadOnlySpan<int> off)
    {
        off.CopyTo(state.Leaving.AsSpan(state.ForcedOff));
        return NbaNodes.SubstitutedOn;
    }

    public static Next<NbaState> SubstitutedOn(NbaState state, ReadOnlySpan<int> on)
    {
        var team = state.LineupTeam;

        for (var i = 0; i < on.Length; i++)
            Swap(state, team, state.Leaving[i], on[i]);

        if (state.Leaving.Take(on.Length).Any(state.FouledOut))
            state.Bench[team] = [.. state.Bench[team].Where(p => !state.FouledOut(p))];

        state.ForcedOff = 0;
        return NextChange(state);
    }

    private static Next<NbaState> AfterRebound(NbaState state, bool byTeam)
    {
        if (!state.ReboundToOffence)
            return EndOfPossession(state, byTeam ? PlayStart.TeamDefensiveRebound : PlayStart.DefensiveRebound);

        state.ShotClock = Graph.ShotClock.OffensiveRebound;
        StartPlay(state, byTeam ? PlayStart.TeamOffensiveRebound : PlayStart.OffensiveRebound);
        return NextPlay(state);
    }

    private static Next<NbaState> NextPlay(NbaState state) => AskTimeouts(state, Moment.Play);

    private static Next<NbaState> AskTimeouts(NbaState state, Moment moment)
    {
        state.Moment = moment;
        state.TimeoutJustCalled = false;
        state.TimeoutCalledHere = false;
        return OffenceTimeoutOrOn(state);
    }

    private static Next<NbaState> OffenceTimeoutOrOn(NbaState state) =>
        Timeouts.Available(state, state.TeamInPossession) ? NbaNodes.OffenceTimeout : DefenceTimeoutOrOn(state);

    private static Next<NbaState> DefenceTimeoutOrOn(NbaState state) =>
        Timeouts.Available(state, Sides.Opponent(state.TeamInPossession)) ? NbaNodes.DefenceTimeout : AfterTimeouts(state);

    private static Next<NbaState> CallTimeout(NbaState state, int team)
    {
        state.TimeoutsLeft[team]--;
        state.TimeoutsThisPeriod[team]++;

        if (state.PeriodClock <= Timeouts.LastThreeMinutes)
            state.TimeoutsInLastThreeMinutes[team]++;

        state.TimeoutJustCalled = true;
        state.TimeoutCalledHere = true;
        state.AfterTimeout |= state.Moment == Moment.Play;
        return OffenceTimeoutOrOn(state);
    }

    private static Next<NbaState> AfterTimeouts(NbaState state)
    {
        state.TimeoutJustCalled = false;
        return NbaNodes.Substitutions;
    }

    private static Next<NbaState> NextChange(NbaState state)
    {
        if ((state.SubstitutingTeams & SubstitutingTeams.Offence) != 0)
            return Change(state, SubstitutingTeams.Offence, state.TeamInPossession);

        if ((state.SubstitutingTeams & SubstitutingTeams.Defence) != 0)
            return Change(state, SubstitutingTeams.Defence, Sides.Opponent(state.TeamInPossession));

        return AfterSubstitutions(state);
    }

    private static Next<NbaState> Change(NbaState state, int side, int team)
    {
        state.SubstitutingTeams &= ~side;

        var court = state.OnCourt[team];
        var bench = state.Bench[team].Length;

        if (bench == 0)
            return NextChange(state);

        state.LineupTeam = team;
        state.ForcedOff = 0;

        foreach (var player in court)
        {
            if (state.ForcesFoulOuts && state.FouledOut(player) && state.ForcedOff < bench)
                state.Leaving[state.ForcedOff++] = player;
        }

        state.Removable = state.ForcedOff == 0 ? court : [.. court.Where(p => Array.IndexOf(state.Leaving, p, 0, state.ForcedOff) < 0)];
        return NbaNodes.SubstitutionCount;
    }

    private static void Swap(NbaState state, int team, int off, int on)
    {
        var now = state.Elapsed;

        state.OnCourt[team][Array.IndexOf(state.OnCourt[team], off)] = on;
        state.Bench[team][Array.IndexOf(state.Bench[team], on)] = off;
        state.SecondsPlayed[off] += now - state.ChangedAt[off];
        state.ChangedAt[off] = now;
        state.ChangedAt[on] = now;
        state.IsOnCourt[off] = false;
        state.IsOnCourt[on] = true;
    }

    private static Next<NbaState> AfterSubstitutions(NbaState state)
    {
        var moment = state.Moment;

        state.Moment = Moment.NotSet;

        return moment switch
        {
            Moment.Play => NbaNodes.AwardedFreeThrow,
            Moment.FreeThrow => NbaNodes.FreeThrowMade,
            _ => throw new InvalidOperationException($"No moment to return to after substitutions: {moment}"),
        };
    }

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
        {
            ReportPlayers(state, Periods.Elapsed(state.Period - 1, 0));
            return Next<NbaState>.GameOver;
        }

        StartPeriod(state);

        if (state.Period >= Periods.Regulation)
        {
            state.TeamInPossession = Sides.Home;
            return NbaNodes.JumpBall;
        }

        state.TeamInPossession = state.Period == Periods.Regulation - 1 ? state.FirstPeriodStarter : Sides.Opponent(state.FirstPeriodStarter);
        return NextPlay(state);
    }

    private static void ReportPlayers(NbaState state, int now)
    {
        for (var team = 0; team < state.Active.Length; team++)
        {
            foreach (var player in state.Active[team])
                state.Incidents.Played(new PlayedIncident(player, team, state.Started[player], state.SecondsOnCourt(player, now), state.Rebounds[player], state.Turnovers[player], state.PersonalFouls[player]));
        }
    }

    private static void ReportPossession(NbaState state) =>
        state.Incidents.Possession(new PossessionIncident(state.TeamInPossession, state.Period, state.PossessionPoints));

    private static void StartPeriod(NbaState state)
    {
        state.PeriodClock = Periods.SecondsIn(state.Period);
        Array.Clear(state.TeamFouls);
        Array.Clear(state.TeamFoulsInLastTwoMinutes);
        Array.Clear(state.TimeoutsThisPeriod);
        Array.Clear(state.TimeoutsInLastThreeMinutes);

        if (state.Period >= Periods.Regulation)
            Array.Fill(state.TimeoutsLeft, Timeouts.PerOvertime);

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
        state.ReboundToOffence = false;
        state.BetweenFreeThrows = false;
        state.AfterTimeout = false;
    }

    private static void DefenceFouls(NbaState state)
    {
        var defence = Sides.Opponent(state.TeamInPossession);

        state.TeamFouls[defence]++;

        if (state.PeriodClock <= Periods.LastTwoMinutes)
            state.TeamFoulsInLastTwoMinutes[defence]++;
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
