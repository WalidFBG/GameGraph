namespace GameGraph.Engine;

public static class GameWalker
{
    public static void Walk<TState, TIncidentCollector>(
        GameSpec<TState, TIncidentCollector> spec,
        TState state,
        IOutcomeDecider<TState, TIncidentCollector> decider,
        IDecisionCollector<TState, TIncidentCollector> decisionCollector,
        TIncidentCollector incidentCollector)
    {
        var node = spec.Start;

        while (node != NodeSpec<TState, TIncidentCollector>.End)
        {
            var features = new double[node.Features.Length];

            for (var i = 0; i < features.Length; i++)
                features[i] = node.Features[i].Compute(state);

            var outcome = decider.Decide(node, state, features);

            decisionCollector.Collect(node, state, features, outcome);

            var next = node.Transition(state, outcome, incidentCollector);

            if (next != NodeSpec<TState, TIncidentCollector>.End && !spec.Nodes.Contains(next))
                throw new InvalidOperationException($"The transition from {node.Name} returned {next.Name}, which is not one of the game's nodes");

            node = next;
        }
    }
}
