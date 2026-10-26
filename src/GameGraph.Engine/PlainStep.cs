namespace GameGraph.Engine;

internal struct PlainStep<TState>(IOutcomeDecider<TState> decider, IDecisionCollector<TState> decisionCollector) : IWalkStep<TState>
{
    private double[] _buffer = [];

    public int Decide(NodeSpec<TState> node, TState state)
    {
        var features = node.WriteFeatures(state, ref _buffer);
        var outcome = decider.Decide(node, state, features);

        decisionCollector.Collect(node, state, features, outcome);

        return outcome;
    }
}
