namespace GameGraph.Nba.Incidents;

public interface INbaIncidentCollector
{
    void Possession(in PossessionIncident incident);
}
