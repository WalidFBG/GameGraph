using GameGraph.Nba.Fixture;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Simulation.Compiled;
using GameGraph.Simulation.Randomness;
using GameGraph.Simulation;
using Xunit;

namespace GameGraph.Nba.Tests.Unit;

public sealed class CompiledWalkTests
{
    private const int Simulations = 1_000;

    private static readonly FittedGraph<NbaState> Fitted = SimulationFixture.Fitted();
    private static readonly CompiledGraph<NbaState> Compiled = new(Fitted);
    private static readonly GameContext Context = SimulationFixture.Context();

    [Fact]
    public void TheCompiledParametersAreThePlainOnesBitForBitAtEveryDecision()
    {
        var check = new CompiledParameterCheck(Fitted, Compiled);

        Simulator.Run(Fitted, Seeds.Draw(Simulations), _ => new NbaState(new EmptyIncidentCollector(), Context), check);

        Assert.True(check.Decisions > 0);
        Assert.Empty(check.Mismatches.Take(10));
    }

    [Fact]
    public void TheCompiledWalkDecidesAndReportsAsThePlainWalk()
    {
        var seeds = Seeds.Draw(Simulations);
        var plain = new SimulationDigest();
        var compiled = new SimulationDigest();

        Simulator.Run(Fitted, seeds, _ => new NbaState(plain, Context), new PlainOutcomes(plain));
        Simulator.Run(Compiled, seeds, _ => new NbaState(compiled, Context), new CompiledOutcomes(compiled));

        Assert.Empty(NbaGraph.Instance.Nodes.Where(n => compiled.Decisions(n) == 0).Select(n => n.Name));
        Assert.Equal(plain.Hex(), compiled.Hex());
    }

    [Fact]
    public void EachCompiledSimulationIsTheSameOnOneThreadOrMany()
    {
        var seeds = Seeds.Draw(Simulations);

        Assert.Equal(Digests(seeds, sequential: true), Digests(seeds, sequential: false));
    }

    private static string[] Digests(ulong[] seeds, bool sequential)
    {
        var digests = new string[seeds.Length];

        void Simulate(int i)
        {
            var digest = new SimulationDigest();

            Simulator.Run(Compiled, [seeds[i]], _ => new NbaState(digest, Context), new CompiledOutcomes(digest));
            digests[i] = digest.Hex();
        }

        if (sequential)
        {
            for (var i = 0; i < seeds.Length; i++)
                Simulate(i);
        }
        else
        {
            Parallel.For(0, seeds.Length, Simulate);
        }

        return digests;
    }
}
