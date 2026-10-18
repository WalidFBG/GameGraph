namespace GameGraph.Nba.Graph;

public static class Sides
{
    public const int Home = 0;
    public const int Away = 1;

    public static int Opponent(int side) => 1 - side;

    public static string Name(int side) => side switch
    {
        Home => nameof(Home),
        Away => nameof(Away),
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Not a side"),
    };
}
