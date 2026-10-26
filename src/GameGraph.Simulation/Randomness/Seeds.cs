namespace GameGraph.Simulation.Randomness;

public static class Seeds
{
    public const ulong Root = 1;

    public static ulong[] Draw(int count)
    {
        var root = new SplitMix64(Root);
        var seeds = new ulong[count];

        for (var i = 0; i < count; i++)
            seeds[i] = root.Next();

        return seeds;
    }
}
