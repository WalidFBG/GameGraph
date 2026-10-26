namespace GameGraph.Nba.Incidents;

public readonly record struct PlayedIncident(int Player, int Team, bool Started, int Seconds, int Rebounds, int Turnovers, int Fouls);
