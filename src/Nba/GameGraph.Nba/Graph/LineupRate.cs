namespace GameGraph.Nba.Graph;

public static class LineupRate
{
    public const int TwoAttempts = 0;
    public const int ThreeAttempts = 1;
    public const int FoulsDrawn = 2;
    public const int OffensiveRebounds = 3;
    public const int DefensiveRebounds = 4;
    public const int Steals = 5;
    public const int Blocks = 6;
    public const int ShootingFouls = 7;
    public const int Count = 8;

    public static double[][] Sources(PlayerRates rates) =>
        [rates.TwoAttempts, rates.ThreeAttempts, rates.FoulsDrawn, rates.OffensiveRebounds, rates.DefensiveRebounds, rates.Steals, rates.Blocks, rates.ShootingFouls];
}
