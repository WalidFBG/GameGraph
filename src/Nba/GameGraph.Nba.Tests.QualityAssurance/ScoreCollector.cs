using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class ScoreCollector(Scores scores, int simulationIndex) : INbaIncidentCollector
{
    public void Possession(in PossessionIncident incident)
    {
        scores.Points[simulationIndex, incident.TeamInPossession] += incident.Points;
        scores.Possessions[simulationIndex]++;
    }
}
