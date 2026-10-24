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
        Next<TState, TIncidentCollector> next = spec.Start;

        while (next is NextNode<TState, TIncidentCollector>(var node))
        {
            var features = new double[node.Features.Length];

            for (var i = 0; i < features.Length; i++)
                features[i] = node.Features[i].Compute(state);

            var outcome = decider.Decide(node, state, features);

            decisionCollector.Collect(node, state, features, outcome);

            next = node.Transition(state, outcome, incidentCollector);

            if (next is NextNode<TState, TIncidentCollector>(var target) && !spec.Nodes.Contains(target))
                throw new InvalidOperationException($"The transition from {node.Name} returned {target.Name}, which is not one of the game's nodes");
        }
    }
}
