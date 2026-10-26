namespace GameGraph.Engine;

public static class GameWalker
{
    public static void Walk<TState>(
        GameSpec<TState> spec,
        TState state,
        IOutcomeDecider<TState> decider,
        IDecisionCollector<TState> decisionCollector)
    {
        var step = new PlainStep<TState>(decider, decisionCollector);

        Walk(spec, state, ref step);
    }

    public static void Walk<TState, TStep>(GameSpec<TState> spec, TState state, ref TStep step)
        where TStep : struct, IWalkStep<TState>
    {
        Next<TState> next = spec.Start;

        while (next is NextNode<TState>(var node))
        {
            next = node.Apply(state, step.Decide(node, state));

            if (next is NextNode<TState>(var target) && !spec.Contains(target))
                throw new InvalidOperationException($"The transition from {node.Name} returned {target.Name}, which is not one of the game's nodes");
        }
    }
}
