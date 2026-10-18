namespace GameGraph.Engine;

public static class GameWalker
{
    public static void Walk<TState>(GameSpec<TState> spec, TState state, IOutcomeDecider<TState> decider, IWalkSink<TState> sink)
    {
        var node = spec.Start;

        while (node != NodeSpec<TState>.End)
        {
            var features = new double[node.Features.Length];

            for (var i = 0; i < features.Length; i++)
                features[i] = node.Features[i].Compute(state);

            var outcome = decider.Decide(node, state);

            sink.Observe(node, features, outcome);

            var next = node.Transition(state, outcome);

            if (next != NodeSpec<TState>.End && !spec.Nodes.Contains(next))
                throw new InvalidOperationException($"The transition from {node.Name} returned {next.Name}, which is not one of the game's nodes");

            node = next;
        }
    }
}
