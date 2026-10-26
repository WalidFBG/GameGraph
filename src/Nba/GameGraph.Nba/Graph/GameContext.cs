namespace GameGraph.Nba.Graph;

public sealed record GameContext(
    double HomePointsPerPossession,
    double AwayPointsPerPossession,
    double[][] LogShares,
    PlayerRates Rates,
    double[] UsualSeconds,
    double[] StartShares,
    double[] StartedLastGame,
    int[][] Active);
