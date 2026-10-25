using GameGraph.Contract;

namespace GameGraph.Engine;

public sealed class TrainingDataCollector<TState>(GameSpec<TState> spec) : IDecisionCollector<TState>
{
    private readonly Dictionary<NodeSpec<TState>, List<(string GameId, DateTime StartUtc, List<TrainingExample> Examples)>> _games =
        spec.Nodes.ToDictionary(n => n, _ => new List<(string, DateTime, List<TrainingExample>)>());

    public void BeginGame(string gameId, DateTime startUtc)
    {
        foreach (var games in _games.Values)
            games.Add((gameId, startUtc, []));
    }

    public void Collect(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features, int outcome)
    {
        var games = _games[node];

        if (games.Count == 0)
            throw new InvalidOperationException($"{nameof(BeginGame)} must be called before the walk");

        games[^1].Examples.Add(new TrainingExample(features.ToArray(), outcome));
    }

    public GameExamples[] GamesFor(NodeSpec<TState> node) => [.. _games[node].Select(g => new GameExamples(g.GameId, g.StartUtc, [.. g.Examples]))];
}
