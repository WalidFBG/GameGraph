using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Graph;

public sealed class NbaState(INbaIncidentCollector incidents, double homeScoringRate, double awayScoringRate)
{
    public INbaIncidentCollector Incidents { get; } = incidents;

    public double[] ScoringRate { get; } = [homeScoringRate, awayScoringRate];

    public int[] Score { get; } = new int[2];

    public int[] TeamFouls { get; } = new int[2];

    public int Period { get; set; }

    public int PeriodClock { get; set; } = Periods.SecondsIn(0);

    public int ShotClock { get; set; } = Graph.ShotClock.Full;

    public PlayStart Start { get; set; } = PlayStart.PeriodStart;

    public ActionType Action { get; set; }

    public int FirstPeriodStarter { get; set; }

    public int TeamInPossession { get; set; }

    public int PossessionPoints { get; set; }

    public int ShotPoints { get; set; }

    public bool Fouled { get; set; }

    public int FreeThrowsLeft { get; set; }

    public int AwardedTeam { get; set; }

    public Miss LastMiss { get; set; }

    public bool PeriodClockBinds => PeriodClock < ShotClock + Graph.ShotClock.Slack;

    public int Bound => Math.Min(ShotClock + Graph.ShotClock.Slack, PeriodClock);
}
