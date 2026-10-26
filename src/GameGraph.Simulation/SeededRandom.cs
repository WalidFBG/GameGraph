using System.Numerics;

namespace GameGraph.Simulation;

public sealed class SeededRandom
{
    private const double UnitPerStep = 1.0 / (1UL << 53);

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public SeededRandom(ulong seed)
    {
        var seeding = new SplitMix64(seed);

        _s0 = seeding.Next();
        _s1 = seeding.Next();
        _s2 = seeding.Next();
        _s3 = seeding.Next();
    }

    public double NextDouble() => (Next() >> 11) * UnitPerStep;

    private ulong Next()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        var shifted = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= shifted;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }
}
