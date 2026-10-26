namespace GameGraph.Nba.Incidents;

public sealed class EmptyIncidentCollector : INbaIncidentCollector
{
    public void Possession(in PossessionIncident incident)
    {
    }

    public void AwardedFreeThrow(in AwardedFreeThrowIncident incident)
    {
    }

    public void Played(in PlayedIncident incident)
    {
    }
}
