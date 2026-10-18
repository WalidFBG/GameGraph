namespace GameGraph.Simulation;

public static class Seeds
{
    public const int Root = 1;

    public static int[] Draw(int count)
    {
        var root = new Random(Root);
        var seeds = new int[count];

        for (var i = 0; i < count; i++)
            seeds[i] = root.Next();

        return seeds;
    }
}
