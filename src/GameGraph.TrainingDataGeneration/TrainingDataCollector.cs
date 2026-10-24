using GameGraph.Engine;

namespace GameGraph.TrainingDataGeneration;

public sealed class TrainingDataCollector<TState>(GameSpec<TState> spec) : IDecisionCollector<TState>
{
    private readonly Dictionary<NodeSpec<TState>, List<GameExamples>> _games = spec.Nodes.ToDictionary(n => n, _ => new List<GameExamples>());

    public void BeginGame(string gameId, DateTime startUtc)
    {
        foreach (var games in _games.Values)
            games.Add(new GameExamples(gameId, startUtc, []));
    }

    public void DiscardGame()
    {
        foreach (var games in _games.Values)
        {
            if (games.Count == 0)
                throw new InvalidOperationException($"{nameof(BeginGame)} must be called before a game is discarded");

            games.RemoveAt(games.Count - 1);
        }
    }

    public void Collect(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features, int outcome)
    {
        var games = _games[node];

        if (games.Count == 0)
            throw new InvalidOperationException($"{nameof(BeginGame)} must be called before the walk");

        games[^1].Examples.Add(new TrainingExample(features.ToArray(), outcome));
    }

    public IReadOnlyList<GameExamples> GamesFor(NodeSpec<TState> node) => _games[node];
}
