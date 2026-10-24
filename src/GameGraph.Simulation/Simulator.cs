using GameGraph.Engine;
using GameGraph.Models.Contract;

namespace GameGraph.Simulation;

public static class Simulator
{
    public static void Run<TState>(
        GameSpec<TState> spec,
        IReadOnlyList<FittedModel> fitted,
        int simulations,
        Func<int, TState> createState)
    {
        var models = SimulationModels.For(spec, fitted);
        var seeds = Seeds.Draw(simulations);
        var decisionCollector = new EmptyDecisionCollector<TState>();

        for (var simulationIndex = 0; simulationIndex < simulations; simulationIndex++)
        {
            var decider = new SampledOutcomeDecider<TState>(models, new SeededRandom(seeds[simulationIndex]));

            GameWalker.Walk(spec, createState(simulationIndex), decider, decisionCollector);
        }
    }
}
