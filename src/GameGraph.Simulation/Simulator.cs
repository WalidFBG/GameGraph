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
}
