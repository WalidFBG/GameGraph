using GameGraph.Engine;

namespace GameGraph.Simulation;

public static class Simulator
{
    public static void Run<TState>(
        FittedGame<TState> game,
        IReadOnlyList<ulong> seeds,
        Func<int, TState> createState,
        IDecisionCollector<TState> decisionCollector)
    {
        for (var simulationIndex = 0; simulationIndex < seeds.Count; simulationIndex++)
        {
            var decider = new SampledOutcomeDecider<TState>(game, new SeededRandom(seeds[simulationIndex]));

            GameWalker.Walk(game.Spec, createState(simulationIndex), decider, decisionCollector);
        }
    }

    public static void Run<TState>(CompiledGame<TState> game, IReadOnlyList<ulong> seeds, Func<int, TState> createState) =>
        Run(game, seeds, createState, new NoOutcomeCollector<TState>());

    public static void Run<TState, TCollector>(CompiledGame<TState> game, IReadOnlyList<ulong> seeds, Func<int, TState> createState, TCollector outcomeCollector)
        where TCollector : struct, IOutcomeCollector<TState>
    {
        for (var simulationIndex = 0; simulationIndex < seeds.Count; simulationIndex++)
        {
            var step = new CompiledStep<TState, TCollector>(game, new SeededRandom(seeds[simulationIndex]), outcomeCollector);

            GameWalker.Walk(game.Spec, createState(simulationIndex), ref step);
        }
    }
}
