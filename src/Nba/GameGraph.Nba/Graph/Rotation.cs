namespace GameGraph.Nba.Graph;

public sealed class Rotation
{
    private readonly Lineup[][] _lineups;
    private readonly int _lastPeriod;

    public Rotation(int periods, Lineup[] home, Lineup[] away)
    {
        foreach (var lineups in new[] { home, away })
        {
            if (lineups.Length == 0 || lineups[0].Period != 0 || lineups[0].Clock != Periods.SecondsIn(0))
                throw new ArgumentException("A team's rotation starts with its starting lineup", nameof(home));

            if (lineups.Zip(lineups[1..]).Any(p => p.Second.Period < p.First.Period || (p.Second.Period == p.First.Period && p.Second.Clock > p.First.Clock)))
                throw new ArgumentException("A team's lineups are in the order of the game", nameof(home));

            if (lineups[^1].Period >= periods)
                throw new ArgumentException($"A lineup changes in period {lineups[^1].Period} of a game of {periods}", nameof(periods));
        }

        _lineups = [home, away];
        _lastPeriod = periods - 1;
    }

    public void FillOnCourt(int[][] onCourt, int period, int clock)
    {
        for (var team = 0; team < _lineups.Length; team++)
            FiveAt(team, period, clock).CopyTo(onCourt[team], 0);
    }

    public int[] FiveAt(int team, int period, int clock)
    {
        var lineups = _lineups[team];
        var five = lineups[0].Five;
        var read = Math.Min(period, _lastPeriod);

        foreach (var lineup in lineups)
        {
            if (!lineup.OnCourtAt(read, clock))
                break;

            five = lineup.Five;
        }

        return five;
    }
}
