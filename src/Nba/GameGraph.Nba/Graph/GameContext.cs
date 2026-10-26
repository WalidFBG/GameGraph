namespace GameGraph.Nba.Graph;

public sealed record GameContext(
    double HomePointsPerPossession,
    double AwayPointsPerPossession,
    double[] LogTwoShares,
    double[] LogThreeShares,
    double[] UsualSeconds,
    double[] StartShares,
    double[] StartedLastGame,
    int[][] Active);
