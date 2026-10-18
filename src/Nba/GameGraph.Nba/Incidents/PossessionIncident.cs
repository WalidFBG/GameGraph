namespace GameGraph.Nba.Incidents;

public readonly record struct PossessionIncident(int SimulationIndex, int TeamInPossession, int Period, int Points);
