using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.QualityAssurance;

public sealed class ScoreCollector(int simulations) : INbaIncidentCollector
{
    public int[,] Points { get; } = new int[simulations, 2];

    public int[] Possessions { get; } = new int[simulations];

    public void Possession(in PossessionIncident incident)
    {
        Points[incident.SimulationIndex, incident.TeamInPossession] += incident.Points;
        Possessions[incident.SimulationIndex]++;
    }

    public int Home(int simulationIndex) => Points[simulationIndex, Sides.Home];

    public int Away(int simulationIndex) => Points[simulationIndex, Sides.Away];
}
