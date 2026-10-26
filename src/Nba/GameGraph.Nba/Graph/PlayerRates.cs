namespace GameGraph.Nba.Graph;

public sealed record PlayerRates(
    double[] Assisted,
    double[] Blocked,
    double[] TwoMakes,
    double[] ThreeMakes,
    double[] LogTwoFouledRatio,
    double[] LogThreeFouledRatio,
    double[] FreeThrows,
    double[] TwoAttempts,
    double[] ThreeAttempts,
    double[] FoulsDrawn,
    double[] OffensiveRebounds,
    double[] DefensiveRebounds,
    double[] Steals,
    double[] Blocks,
    double[] ShootingFouls);
