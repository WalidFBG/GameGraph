using GameGraph.Engine;

namespace GameGraph.Simulation;

public static class Simulator
{
    public static void Run<TState>(
        FittedGame<TState> game,
        int simulations,
        Func<int, TState> createState)
    {
        var seeds = Seeds.Draw(simulations);
        var decisionCollector = new EmptyDecisionCollector<TState>();

        for (var simulationIndex = 0; simulationIndex < simulations; simulationIndex++)
        {
            var decider = new SampledOutcomeDecider<TState>(game, new SeededRandom(seeds[simulationIndex]));

            GameWalker.Walk(game.Spec, createState(simulationIndex), decider, decisionCollector);
        }
    }
}
