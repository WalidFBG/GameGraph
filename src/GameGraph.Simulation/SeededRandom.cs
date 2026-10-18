namespace GameGraph.Simulation;

public sealed class SeededRandom(int seed)
{
    private readonly Random _random = new(seed);

    public double NextDouble() => _random.NextDouble();
}
