namespace GameGraph.Nba.Graph;

public static class Periods
{
    public const int Regulation = 4;

    public const int RegulationSeconds = 720;

    public const int OvertimeSeconds = 300;

    public const int LastTwoMinutes = 120;

    public static int SecondsIn(int period) => period < Regulation ? RegulationSeconds : OvertimeSeconds;

    public static int Elapsed(int period, int clock) =>
        Math.Min(period, Regulation) * RegulationSeconds + Math.Max(period - Regulation, 0) * OvertimeSeconds + SecondsIn(period) - clock;
}
