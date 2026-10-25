using System.Globalization;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Sportradar;

internal sealed class RotationParser(Dictionary<long, TeamType> teams, Dictionary<long, int> slots)
{
    private readonly List<Lineup>[] _lineups = [[], []];
    private readonly HashSet<int>?[] _onCourt = [null, null];
    private int _period;

    public void Read(Event e)
    {
        switch (e)
        {
            case { EventType: EventType.EndPeriod }:
                _period++;
                break;
            case LineupChange change:
                Change(change);
                break;
        }
    }

    public Rotation Build(int periods)
    {
        try
        {
            return new Rotation(periods, [.. _lineups[Sides.Home]], [.. _lineups[Sides.Away]]);
        }
        catch (ArgumentException)
        {
            throw new RejectedGameException(RejectionReason.InconsistentLineup);
        }
    }

    private void Change(LineupChange change)
    {
        long[] players = [.. change.PlayerIdsOn, .. change.PlayerIdsOff];
        var sides = players.Select(p => teams.GetValueOrDefault(p)).Distinct().ToArray();

        if (sides is not [TeamType.HomeOrA or TeamType.AwayOrB])
            throw new RejectedGameException(RejectionReason.InconsistentLineup);

        var team = sides[0] == TeamType.HomeOrA ? Sides.Home : Sides.Away;
        var on = change.PlayerIdsOn.Select(p => slots[p]).ToArray();
        var off = change.PlayerIdsOff.Select(p => slots[p]).ToArray();

        if (change.IsStartingLineup != (_onCourt[team] is null))
            throw new RejectedGameException(RejectionReason.InconsistentLineup);

        var court = _onCourt[team] ?? [];

        if (off.Any(p => !court.Contains(p)) || on.Any(court.Contains) || on.Length != off.Length + (change.IsStartingLineup ? Lineup.Size : 0))
            throw new RejectedGameException(RejectionReason.InconsistentLineup);

        court.ExceptWith(off);
        court.UnionWith(on);
        _onCourt[team] = court;
        _lineups[team].Add(new Lineup(_period, Clock(change), [.. court.Order()]));
    }

    private static int Clock(Event e)
    {
        var parts = (e.GameClock ?? throw new InvalidDataException($"Event {e.SourceId} has no game clock")).MinutesIntoPeriod.Split(':');

        return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60 + int.Parse(parts[1], CultureInfo.InvariantCulture);
    }
}
