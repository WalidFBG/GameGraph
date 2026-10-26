using GameGraph.Contract;
using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Walk;

public sealed class ExampleRecorder<TState>(Graph<TState> graph)
{
    private readonly List<(string GameId, DateTime StartUtc, List<TrainingExample>[] Examples)> _games = [];

    public IDecisionCollector<TState> BeginGame(string gameId, DateTime startUtc)
    {
        List<TrainingExample>[] examples = [.. graph.Nodes.Select(_ => new List<TrainingExample>())];

        _games.Add((gameId, startUtc, examples));

        return new GameRecording(graph, examples);
    }

    public GameExamples[] GamesFor(Node<TState> node)
    {
        var index = graph.IndexOf(node);

        return [.. _games.Select(g => new GameExamples(g.GameId, g.StartUtc, [.. g.Examples[index]]))];
    }

    private sealed class GameRecording(Graph<TState> graph, List<TrainingExample>[] examples) : IDecisionCollector<TState>
    {
        public void Collect(Node<TState> node, TState state, ReadOnlySpan<double> features, int outcome)
        {
            if (node.HasOneOutcome(state))
                return;

            examples[graph.IndexOf(node)].Add(new TrainingExample(features.ToArray(), outcome));
        }
    }
}
