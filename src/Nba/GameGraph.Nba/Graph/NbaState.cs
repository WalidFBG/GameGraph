namespace GameGraph.Nba.Graph;

public sealed class NbaState
{
    public NbaState(PeriodSchedule[] periods, double homeScoringRate, double awayScoringRate)
    {
        if (periods.Length == 0)
            throw new ArgumentException("A game needs at least one period", nameof(periods));

        Periods = periods;
        ScoringRate = [homeScoringRate, awayScoringRate];
        Side = periods[0].StartingSide;
        RemainingInPeriod = periods[0].Possessions;
    }

    public PeriodSchedule[] Periods { get; }

    public double[] ScoringRate { get; }

    public int[] Score { get; } = new int[2];

    public int Period { get; set; }

    public int Side { get; set; }

    public int RemainingInPeriod { get; set; }
}
