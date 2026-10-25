namespace GameGraph.Nba.Graph;

public sealed record GameContext(double HomePointsPerPossession, double AwayPointsPerPossession, double[] LogTwoShares, double[] LogThreeShares);
