using GameGraph.Engine;

namespace GameGraph.TrainingDataGeneration;

public sealed class TrainingDataCollector<TState>(GameSpec<TState> spec) : IWalkSink<TState>
{
    private readonly Dictionary<NodeSpec<TState>, List<TrainingExample>> _examples = spec.Nodes.ToDictionary(n => n, _ => new List<TrainingExample>());

    public void Observe(NodeSpec<TState> node, double[] features, int outcome) =>
        _examples[node].Add(new TrainingExample(features, outcome));

    public IReadOnlyList<TrainingExample> ExamplesFor(NodeSpec<TState> node) => _examples[node];
}
