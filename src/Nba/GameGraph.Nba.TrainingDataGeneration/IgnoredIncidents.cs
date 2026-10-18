using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class IgnoredIncidents : INbaIncidentCollector
{
    public void Possession(in PossessionIncident incident)
    {
    }
}
