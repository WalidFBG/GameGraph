using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class EmptyIncidentCollector : INbaIncidentCollector
{
    public void Possession(in PossessionIncident incident)
    {
    }
}
