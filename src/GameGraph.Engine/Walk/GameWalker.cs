using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Walk;

public static class GameWalker
{
    public static void Walk<TState>(
        Graph<TState> graph,
        TState state,
        IOutcomeDecider<TState> decider,
        IDecisionCollector<TState> decisionCollector)
    {
        var step = new PlainStep<TState>(decider, decisionCollector);

        Walk(graph, state, ref step);
    }

    public static void Walk<TState, TStep>(Graph<TState> graph, TState state, ref TStep step)
        where TStep : struct, IWalkStep<TState>
    {
        Next<TState> next = graph.Start;

        while (next is NextNode<TState>(var node))
        {
            next = node.Apply(state, step.Decide(node, state));

            if (next is NextNode<TState>(var target) && !graph.Contains(target))
                throw new InvalidOperationException($"The transition from {node.Name} returned {target.Name}, which is not one of the graph's nodes");
        }
    }
}
