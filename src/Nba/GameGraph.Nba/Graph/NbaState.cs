using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public sealed class NbaState
{
    public NbaState(INbaIncidentCollector incidents, GameContext context)
    {
        Incidents = incidents;
        PointsPerPossession = [context.HomePointsPerPossession, context.AwayPointsPerPossession];
        LogShares = context.LogShares;
        AssistedRates = context.AssistedRates;
        BlockedRates = context.BlockedRates;
        UsualSeconds = context.UsualSeconds;
        StartShares = context.StartShares;
        StartedLastGame = context.StartedLastGame;
        Active = context.Active;
        Bench = [.. Active.Select(a => a.Length > Lineups.Size ? new int[a.Length - Lineups.Size] : throw new ArgumentException($"A team needs more than {Lineups.Size} active players, got {a.Length}", nameof(context)))];
        IsOnCourt = new bool[UsualSeconds.Length];
        Started = new bool[UsualSeconds.Length];
        ChangedAt = new int[UsualSeconds.Length];
        SecondsPlayed = new int[UsualSeconds.Length];
        Rebounds = new int[UsualSeconds.Length];
        Turnovers = new int[UsualSeconds.Length];
        PersonalFouls = new int[UsualSeconds.Length];
        Points = new int[UsualSeconds.Length];
        FieldGoalAttempts = new int[UsualSeconds.Length];
        ThreePointAttempts = new int[UsualSeconds.Length];
        FreeThrowAttempts = new int[UsualSeconds.Length];
        Assists = new int[UsualSeconds.Length];
        Steals = new int[UsualSeconds.Length];
        Blocks = new int[UsualSeconds.Length];
    }

    public INbaIncidentCollector Incidents { get; }

    public double[] PointsPerPossession { get; }

    public double[][] LogShares { get; }

    public double[] AssistedRates { get; }

    public double[] BlockedRates { get; }

    public double[] UsualSeconds { get; }

    public double[] StartShares { get; }

    public double[] StartedLastGame { get; }

    public int[][] Active { get; }

    public int[][] OnCourt { get; } = [new int[Lineups.Size], new int[Lineups.Size]];

    public int[][] Bench { get; }

    public bool[] IsOnCourt { get; }

    public bool[] Started { get; }

    public int[] ChangedAt { get; }

    public int[] SecondsPlayed { get; }

    public int[] Rebounds { get; }

    public int[] Turnovers { get; }

    public int[] PersonalFouls { get; }

    public int[] Points { get; }

    public int[] FieldGoalAttempts { get; }

    public int[] ThreePointAttempts { get; }

    public int[] FreeThrowAttempts { get; }

    public int[] Assists { get; }

    public int[] Steals { get; }

    public int[] Blocks { get; }

    public int[] Teammates { get; } = new int[Lineups.Size - 1];

    public int Shooter { get; set; }

    public int TurnoverKind { get; set; }

    public int LineupTeam { get; set; } = Sides.Home;

    public int SubstitutingTeams { get; set; }

    public int ChangeSize { get; set; }

    public int[] Leaving { get; } = new int[Lineups.Size];

    public int ForcedOff { get; set; }

    public int[] Removable { get; set; } = [];

    public int[] Score { get; } = new int[2];

    public int[] TimeoutsLeft { get; } = [Timeouts.PerGame, Timeouts.PerGame];

    public int Period { get; set; }

    public int FirstPeriodStarter { get; set; }

    public int PeriodClock { get; set; } = Periods.SecondsIn(0);

    public int[] TeamFouls { get; } = new int[2];

    public int[] TeamFoulsInLastTwoMinutes { get; } = new int[2];

    public int[] TimeoutsThisPeriod { get; } = new int[2];

    public int[] TimeoutsInLastThreeMinutes { get; } = new int[2];

    public int TeamInPossession { get; set; }

    public int PossessionPoints { get; set; }

    public int ShotClock { get; set; } = Graph.ShotClock.Full;

    public PlayStart Start { get; set; } = PlayStart.PeriodStart;

    public ActionType Action { get; set; }

    public int ShotPoints { get; set; }

    public bool Fouled { get; set; }

    public int FreeThrowsLeft { get; set; }

    public Miss LastMiss { get; set; }

    public bool ReboundToOffence { get; set; }

    public bool AfterTimeout { get; set; }

    public Moment Moment { get; set; }

    public bool TimeoutJustCalled { get; set; }

    public bool TimeoutCalledHere { get; set; }

    public bool BetweenFreeThrows { get; set; }

    public int AwardedTeam { get; set; }

    public int LastAwarded { get; set; } = AwardedFreeThrows.None;

    public bool BallAwardedToDefence { get; set; }

    public bool PeriodClockBinds => PeriodClock < ShotClock + Graph.ShotClock.Slack;

    public int Bound => Math.Min(ShotClock + Graph.ShotClock.Slack, PeriodClock);

    public int Elapsed => Periods.Elapsed(Period, PeriodClock);

    public int SecondsOnCourt(int player, int now) => SecondsPlayed[player] + (IsOnCourt[player] ? now - ChangedAt[player] : 0);

    public bool IsInFoulTrouble(int player) => PersonalFouls[player] >= Math.Min(Period, Periods.Regulation - 1) + 2;

    public int InFoulTrouble(int team) => OnCourt[team].Count(IsInFoulTrouble);

    public bool FouledOut(int player) => PersonalFouls[player] >= Lineups.FoulOut;

    public bool ForcesFoulOuts => Moment == Moment.Play && PeriodClock > 0;

    public bool MustChange(int team) => ForcesFoulOuts && Bench[team].Length > 0 && OnCourt[team].Any(FouledOut);

    public int LongestStint(int team) => Elapsed - OnCourt[team].Min(p => ChangedAt[p]);
}
