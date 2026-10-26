using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Tests.Unit;

internal static class ObservedGames
{
    public const int FinalPeriod = Periods.Regulation - 1;

    private const int Step = 20;
    private const int EndingFrom = 40;
    private const int Players = 16;

    public static int[] Starters(int team) => [.. Enumerable.Range(team * 8, Lineups.Size)];

    public static int[] Bench(int team) => [.. Enumerable.Range(team * 8 + Lineups.Size, 8 - Lineups.Size)];

    public static AcceptedGame Ending(Func<int, Possession[]> ending)
    {
        var possessions = new List<Possession>();

        for (var period = 0; period < Periods.Regulation; period++)
        {
            var team = period is 0 or FinalPeriod ? Sides.Home : Sides.Away;
            var last = period == FinalPeriod ? EndingFrom : 0;

            for (var clock = Periods.RegulationSeconds - Step; clock >= last; clock -= Step)
            {
                possessions.Add(new Possession(team, period, [Made(team, clock, points: team == Sides.Home && period == FinalPeriod ? 3 : 2)]));
                team = Sides.Opponent(team);
            }

            if (period == FinalPeriod)
                possessions.AddRange(ending(team));
        }

        var roster = new Roster([[.. Starters(Sides.Home), .. Bench(Sides.Home)], [.. Starters(Sides.Away), .. Bench(Sides.Away)]], [Starters(Sides.Home), Starters(Sides.Away)], new int[Players]);

        return new AcceptedGame("hand-made", new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), 1, 2, [.. Enumerable.Range(100, Players).Select(id => (long)id)], roster, [.. possessions], 0);
    }

    public static ObservedShot Made(int team, int clock, int points = 2) => new(clock, Starters(team)[0], points, null, true, null, null, [], null);

    public static ObservedShot Missed(int team, int clock, ObservedRebound? rebound) => new(clock, Starters(team)[0], 2, null, false, null, null, [], rebound);

    public static ObservedMoment Substitution(int team, int off, int on) => new([], [new ObservedSubstitution(team, [off], [on])]);

    public static NbaState State(AcceptedGame game)
    {
        var unread = new double[game.Players.Length];
        double[][] shares = [.. Enum.GetValues<PlayerEvent>().Select(_ => unread)];
        var rates = new PlayerRates(unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread);

        return new NbaState(new EmptyIncidentCollector(), new GameContext(0, 0, shares, rates, unread, unread, unread, game.Roster.Active));
    }
}
