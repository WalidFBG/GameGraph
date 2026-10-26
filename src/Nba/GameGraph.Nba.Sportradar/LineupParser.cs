using System.Globalization;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Sportradar;

internal sealed class LineupParser(Dictionary<long, TeamType> teams, Dictionary<long, int> slots, int[][] active)
{
    private readonly int[]?[] _starters = [null, null];
    private readonly HashSet<int>[] _onCourt = [[], []];
    private readonly HashSet<int>?[] _before = [null, null];
    private readonly int[] _since = new int[slots.Count];
    private readonly int[] _seconds = new int[slots.Count];
    private int _period;

    public bool HasChanges => _before.Any(b => b is not null);

    public Roster Build()
    {
        if (_starters is not [{ } home, { } away])
            throw new RejectedGameException(RejectionReason.InconsistentLineup);

        try
        {
            return new Roster(active, [home, away], [.. _seconds]);
        }
        catch (ArgumentException)
        {
            throw new RejectedGameException(RejectionReason.InconsistentLineup);
        }
    }

    public void Read(Event e)
    {
        switch (e)
        {
            case { EventType: EventType.EndPeriod }:
                EndPeriod();
                break;
            case LineupChange change:
                Change(change);
                break;
        }
    }

    public ObservedSubstitution[] Take()
    {
        var substitutions = new List<ObservedSubstitution>();

        for (var team = 0; team < _before.Length; team++)
        {
            if (_before[team] is not { } before)
                continue;

            _before[team] = null;

            if (!before.SetEquals(_onCourt[team]))
                substitutions.Add(new ObservedSubstitution(team, [.. before.Except(_onCourt[team]).Order()], [.. _onCourt[team].Except(before).Order()]));
        }

        return [.. substitutions];
    }

    private void EndPeriod()
    {
        foreach (var player in _onCourt.SelectMany(c => c))
        {
            _seconds[player] += _since[player];
            _since[player] = Periods.SecondsIn(_period + 1);
        }

        _period++;
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
        var court = _onCourt[team];
        var clock = Clock(change);

        if (change.IsStartingLineup != (_starters[team] is null) || (change.IsStartingLineup && (_period != 0 || clock != Periods.SecondsIn(0))))
            throw new RejectedGameException(RejectionReason.InconsistentLineup);

        if (off.Any(p => !court.Contains(p) || _since[p] < clock) || on.Any(p => court.Contains(p) || !active[team].Contains(p)) || on.Length != off.Length + (change.IsStartingLineup ? Lineups.Size : 0))
            throw new RejectedGameException(RejectionReason.InconsistentLineup);

        if (change.IsStartingLineup)
            _starters[team] = [.. on.Order()];
        else
            _before[team] ??= [.. court];

        foreach (var player in off)
            _seconds[player] += _since[player] - clock;

        foreach (var player in on)
            _since[player] = clock;

        court.ExceptWith(off);
        court.UnionWith(on);
    }

    private static int Clock(Event e)
    {
        var parts = (e.GameClock ?? throw new InvalidDataException($"Event {e.SourceId} has no game clock")).MinutesIntoPeriod.Split(':');

        return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60 + int.Parse(parts[1], CultureInfo.InvariantCulture);
    }
}
