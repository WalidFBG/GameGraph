namespace GameGraph.Nba.Observed;

public readonly record struct Possession(int TeamInPossession, int Period, bool Scored);
