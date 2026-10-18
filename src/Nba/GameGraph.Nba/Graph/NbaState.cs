namespace GameGraph.Nba.Graph;

public sealed class NbaState
{
    public NbaState(int simulationIndex, PeriodSchedule[] periods, double homeScoringRate, double awayScoringRate)
    {
        if (periods.Length == 0)
            throw new ArgumentException("A game needs at least one period", nameof(periods));

        SimulationIndex = simulationIndex;
        Periods = periods;
        ScoringRate = [homeScoringRate, awayScoringRate];
        TeamInPossession = periods[0].StartingTeamInPossession;
        RemainingInPeriod = periods[0].Possessions;
    }

    public int SimulationIndex { get; }

    public PeriodSchedule[] Periods { get; }

    public double[] ScoringRate { get; }

    public int[] Score { get; } = new int[2];

    public int[] TeamFouls { get; } = new int[2];

    public int Period { get; set; }

    public int TeamInPossession { get; set; }

    public int RemainingInPeriod { get; set; }

    public int PossessionPoints { get; set; }

    public int ShotPoints { get; set; }

    public bool Fouled { get; set; }

    public int FreeThrowsLeft { get; set; }

    public Miss LastMiss { get; set; }
}
