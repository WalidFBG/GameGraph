namespace GameGraph.Nba.Incidents;

public readonly record struct PlayedIncident(int Player, int Team, bool Started, int Seconds, int Rebounds, int Turnovers, int Fouls, int Points, int FieldGoalAttempts, int ThreePointAttempts, int FreeThrowAttempts, int Assists, int Steals, int Blocks);
