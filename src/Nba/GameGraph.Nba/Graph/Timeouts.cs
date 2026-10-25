namespace GameGraph.Nba.Graph;

public static class Timeouts
{
    public const int PerGame = 7;

    public const int PerOvertime = 2;

    public const int InFourthPeriod = 4;

    public const int LastThreeMinutes = 180;

    public const int InLastThreeMinutes = 2;

    public static bool Available(NbaState state, int team) =>
        state.TimeoutsLeft[team] > 0
        && (state.Period != Periods.Regulation - 1
            || (state.TimeoutsThisPeriod[team] < InFourthPeriod
                && (state.PeriodClock > LastThreeMinutes || state.TimeoutsInLastThreeMinutes[team] < InLastThreeMinutes)));
}
