using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class ScoreCollector(Scores scores, int simulationIndex) : INbaIncidentCollector
{
    public void Possession(in PossessionIncident incident)
    {
        scores.Points[simulationIndex, incident.TeamInPossession] += incident.Points;
        scores.Possessions[simulationIndex]++;
        scores.Periods[simulationIndex] = Math.Max(scores.Periods[simulationIndex], incident.Period + 1);
    }

    public void AwardedFreeThrow(in AwardedFreeThrowIncident incident)
    {
        if (incident.Made)
            scores.Points[simulationIndex, incident.Team]++;
    }
}
