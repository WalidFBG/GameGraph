using GameGraph.Nba.Audit.Definitions;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Audit.Domain;

internal sealed class GameScore : INbaIncidentCollector
{
    private readonly int[] _points = new int[2];
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

    public GameTotals Totals() => new(_points[Sides.Home], _points[Sides.Away], _possessions, _periods);
}
