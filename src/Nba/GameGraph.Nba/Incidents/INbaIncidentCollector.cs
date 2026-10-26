namespace GameGraph.Nba.Incidents;

public interface INbaIncidentCollector
{
    void Possession(in PossessionIncident incident);

    void AwardedFreeThrow(in AwardedFreeThrowIncident incident);

    void Played(in PlayedIncident incident);
}
