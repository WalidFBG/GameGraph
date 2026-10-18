using GameGraph.Engine;

namespace GameGraph.TrainingDataGeneration;

public sealed class TrainingDataCollector<TState, TIncidentCollector>(GameSpec<TState, TIncidentCollector> spec) : IDecisionCollector<TState, TIncidentCollector>
{
    private readonly Dictionary<NodeSpec<TState, TIncidentCollector>, List<GameExamples>> _games = spec.Nodes.ToDictionary(n => n, _ => new List<GameExamples>());

    public void BeginGame(string gameId, DateTime startUtc)
    {
        foreach (var games in _games.Values)
            games.Add(new GameExamples(gameId, startUtc, []));
    }

    public void Collect(NodeSpec<TState, TIncidentCollector> node, TState state, double[] features, int outcome)
    {
        var games = _games[node];

        if (games.Count == 0)
            throw new InvalidOperationException($"{nameof(BeginGame)} must be called before the walk");

        games[^1].Examples.Add(new TrainingExample(features, outcome));
    }

    public IReadOnlyList<GameExamples> GamesFor(NodeSpec<TState, TIncidentCollector> node) => _games[node];
}
