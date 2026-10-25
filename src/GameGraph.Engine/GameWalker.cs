namespace GameGraph.Engine;

public static class GameWalker
{
    public static void Walk<TState>(
        GameSpec<TState> spec,
        TState state,
        IOutcomeDecider<TState> decider,
        IDecisionCollector<TState> decisionCollector)
    {
        Next<TState> next = spec.Start;
        var buffer = Array.Empty<double>();

        while (next is NextNode<TState>(var node))
        {
            var count = node.FeatureValueCount(state);

            if (count > buffer.Length)
                buffer = new double[count];

            var features = buffer.AsSpan(0, count);

            node.WriteFeatures(state, features);

            var outcome = decider.Decide(node, state, features);

            decisionCollector.Collect(node, state, features, outcome);

            next = node.Transition(state, outcome);

            if (next is NextNode<TState>(var target) && !spec.Contains(target))
                throw new InvalidOperationException($"The transition from {node.Name} returned {target.Name}, which is not one of the game's nodes");
        }
    }
}
