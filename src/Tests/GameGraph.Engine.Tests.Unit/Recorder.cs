namespace GameGraph.Engine.Tests.Unit;

internal sealed class Recorder : IOutcomeDecider<ToyState>, IDecisionCollector<ToyState>
{
    public List<double[]> Decided { get; } = [];

    public List<(string Node, double[] Features, int Outcome)> Collected { get; } = [];

    public int Decide(NodeSpec<ToyState> node, ToyState state, ReadOnlySpan<double> features)
    {
        Decided.Add(features.ToArray());
        return state.Value % 2;
    }

    public void Collect(NodeSpec<ToyState> node, ToyState state, ReadOnlySpan<double> features, int outcome) =>
        Collected.Add((node.Name, features.ToArray(), outcome));
}
