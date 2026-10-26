using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Replay;

internal sealed class ObservedStoppage
{
    private readonly Queue<int> _timeouts;
    private readonly Dictionary<int, ObservedSubstitution> _changes;
    private readonly Queue<AwardedFreeThrow> _awarded;

    public ObservedStoppage(ObservedMoment moment, AwardedFreeThrow[] awarded)
    {
        var teams = moment.Substitutions.Select(c => c.Team).ToArray();

        if (teams.Distinct().Count() != teams.Length)
            throw new RulesDisagreeWithDataException($"A stoppage changes team {string.Join(", ", teams)}, but the rules change each team once a stoppage");

        _timeouts = new Queue<int>(moment.Timeouts);
        _changes = moment.Substitutions.ToDictionary(c => c.Team);
        _awarded = new Queue<AwardedFreeThrow>(awarded);
    }

    public static ObservedStoppage Empty { get; } = new(ObservedMoment.Quiet, []);

    public bool IsRead => _timeouts.Count == 0 && _changes.Count == 0 && _awarded.Count == 0;

    public int Called(int team)
    {
        if (_timeouts.Count == 0 || _timeouts.Peek() != team)
            return 0;

        _timeouts.Dequeue();
        return 1;
    }

    public int Teams(int offence) => SubstitutingTeams.Of(_changes.ContainsKey(offence), _changes.ContainsKey(Sides.Opponent(offence)));

    public ObservedSubstitution Change(int team) =>
        _changes.TryGetValue(team, out var change)
            ? change
            : throw new RulesDisagreeWithDataException($"The rules change team {team}, but the stoppage has no change for it");

    public ObservedSubstitution TakeChange(int team)
    {
        var change = Change(team);

        _changes.Remove(team);
        return change;
    }

    public AwardedFreeThrow? NextAwarded() => _awarded.Count > 0 ? _awarded.Dequeue() : null;
}
