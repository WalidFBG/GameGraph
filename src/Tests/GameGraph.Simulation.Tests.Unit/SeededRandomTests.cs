using Xunit;

namespace GameGraph.Simulation.Tests.Unit;

public sealed class SeededRandomTests
{
    [Fact]
    public void SplitMix64FromZeroGivesItsPublishedSequence()
    {
        var splitMix = new SplitMix64(0);

        Assert.Equal<ulong>([0xE220A8397B1DCDAF, 0x6E789E6AA1B965F4, 0x06C45D188009454F, 0xF88BB8A8724C81EC], [splitMix.Next(), splitMix.Next(), splitMix.Next(), splitMix.Next()]);
    }

    [Fact]
    public void ASeedGivesTheReferenceXoshiroSequence()
    {
        var random = new SeededRandom(42);

        Assert.Equal<double>([0.08386297105988216, 0.3789802506626686, 0.6800434110281394, 0.9246929453253876], [random.NextDouble(), random.NextDouble(), random.NextDouble(), random.NextDouble()]);
    }
}
