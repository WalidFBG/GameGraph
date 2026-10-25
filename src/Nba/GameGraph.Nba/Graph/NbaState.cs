using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public sealed class NbaState(INbaIncidentCollector incidents, double homePointsPerPossession, double awayPointsPerPossession)
{
    public INbaIncidentCollector Incidents { get; } = incidents;

    public double[] PointsPerPossession { get; } = [homePointsPerPossession, awayPointsPerPossession];

    public int[] Score { get; } = new int[2];

    public int Period { get; set; }

    public int FirstPeriodStarter { get; set; }

    public int PeriodClock { get; set; } = Periods.SecondsIn(0);

    public int[] TeamFouls { get; } = new int[2];

    public int[] TeamFoulsInLastTwoMinutes { get; } = new int[2];

    public int TeamInPossession { get; set; }

    public int PossessionPoints { get; set; }

    public int ShotClock { get; set; } = Graph.ShotClock.Full;

    public PlayStart Start { get; set; } = PlayStart.PeriodStart;

    public ActionType Action { get; set; }

    public int ShotPoints { get; set; }

    public bool Fouled { get; set; }

    public int FreeThrowsLeft { get; set; }

    public Miss LastMiss { get; set; }

    public int AwardedTeam { get; set; }

    public int LastAwarded { get; set; } = AwardedFreeThrows.None;

    public bool BallAwardedToDefence { get; set; }

    public bool PeriodClockBinds => PeriodClock < ShotClock + Graph.ShotClock.Slack;

    public bool LateInGame => Period >= Periods.Regulation - 1 && PeriodClock <= Periods.LastTwoMinutes;

    public int Bound => Math.Min(ShotClock + Graph.ShotClock.Slack, PeriodClock);
}
