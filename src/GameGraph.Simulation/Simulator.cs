using GameGraph.Engine.Walk;
using GameGraph.Simulation.Compiled;
using GameGraph.Simulation.Randomness;

namespace GameGraph.Simulation;

public static class Simulator
{
    public static void Run<TState>(
        FittedGraph<TState> fitted,
        IReadOnlyList<ulong> seeds,
        Func<int, TState> createState,
        IDecisionCollector<TState> decisionCollector)
    {
        for (var simulationIndex = 0; simulationIndex < seeds.Count; simulationIndex++)
        {
            var decider = new SampledOutcomeDecider<TState>(fitted, new SeededRandom(seeds[simulationIndex]));

            GameWalker.Walk(fitted.Graph, createState(simulationIndex), decider, decisionCollector);
        }
    }

    public static void Run<TState>(CompiledGraph<TState> compiled, IReadOnlyList<ulong> seeds, Func<int, TState> createState) =>
        Run(compiled, seeds, createState, new NoOutcomeCollector<TState>());

    public static void Run<TState, TCollector>(CompiledGraph<TState> compiled, IReadOnlyList<ulong> seeds, Func<int, TState> createState, TCollector outcomeCollector)
        where TCollector : struct, IOutcomeCollector<TState>
    {
        for (var simulationIndex = 0; simulationIndex < seeds.Count; simulationIndex++)
        {
            var step = new CompiledStep<TState, TCollector>(compiled, new SeededRandom(seeds[simulationIndex]), outcomeCollector);

            GameWalker.Walk(compiled.Graph, createState(simulationIndex), ref step);
        }
    }
}
