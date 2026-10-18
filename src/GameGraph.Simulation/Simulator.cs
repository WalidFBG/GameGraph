using GameGraph.Engine;
using GameGraph.Parameters;

namespace GameGraph.Simulation;

public static class Simulator
{
    public static void Run<TState, TIncidentCollector>(
        GameSpec<TState, TIncidentCollector> spec,
        IReadOnlyList<NodeParameters> parameters,
        int simulations,
        Func<int, TState> createState,
        TIncidentCollector incidentCollector)
    {
        var aligned = ParameterAlignment.Align(spec, parameters);
        var seeds = Seeds.Draw(simulations);
        var decisionCollector = new EmptyDecisionCollector<TState, TIncidentCollector>();

        for (var simulationIndex = 0; simulationIndex < simulations; simulationIndex++)
        {
            var decider = new SampledOutcomeDecider<TState, TIncidentCollector>(aligned, new SeededRandom(seeds[simulationIndex]));

            GameWalker.Walk(spec, createState(simulationIndex), decider, decisionCollector, incidentCollector);
        }
    }
}
