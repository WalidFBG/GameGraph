using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal sealed class GameScore : INbaIncidentCollector
{
    private static readonly int Credits = Enum.GetValues<Credit>().Length;

    private readonly int[] _points = new int[2];
    private readonly Dictionary<int, PlayedIncident> _played = [];
    private readonly Dictionary<int, int[]> _credits = [];
    private int _possessions;
    private int _periods;

    public void Possession(in PossessionIncident incident)
    {
        _points[incident.TeamInPossession] += incident.Points;
        _possessions++;
        _periods = Math.Max(_periods, incident.Period + 1);
    }

    public void AwardedFreeThrow(in AwardedFreeThrowIncident incident)
    {
        if (incident.Made)
            _points[incident.Team]++;
    }

    public void Credited(in CreditIncident incident)
    {
        if (!_credits.TryGetValue(incident.Player, out var credits))
            _credits[incident.Player] = credits = new int[Credits];

        credits[(int)incident.Credit]++;
    }

    public void Played(in PlayedIncident incident) => _played.Add(incident.Player, incident);

    public GameTotals Totals() =>
        new(_points[Sides.Home], _points[Sides.Away], _possessions, _periods, _played.ToDictionary(p => p.Key, p => Tally(p.Value, _credits.GetValueOrDefault(p.Key) ?? new int[Credits])));

    private static PlayerTotals Tally(PlayedIncident played, int[] credits)
    {
        int Of(Credit credit) => credits[(int)credit];

        var twos = Of(Credit.TwoMade) + Of(Credit.TwoMissed);
        var threes = Of(Credit.ThreeMade) + Of(Credit.ThreeMissed);

        return new PlayerTotals(
            played.Started,
            played.Seconds,
            Rebounds: Of(Credit.OffensiveRebound) + Of(Credit.DefensiveRebound),
            Turnovers: Of(Credit.Turnover),
            Fouls: Of(Credit.Foul),
            Points: 2 * Of(Credit.TwoMade) + 3 * Of(Credit.ThreeMade) + Of(Credit.FreeThrowMade),
            FieldGoalAttempts: twos + threes,
            ThreePointAttempts: threes,
            FreeThrowAttempts: Of(Credit.FreeThrowMade) + Of(Credit.FreeThrowMissed),
            Assists: Of(Credit.Assist),
            Steals: Of(Credit.Steal),
            Blocks: Of(Credit.Block));
    }
}
