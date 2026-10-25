namespace GameGraph.Contract;

public sealed record GameExamples
{
    public GameExamples(string gameId, DateTime startUtc, TrainingExample[] examples)
    {
        GameId = gameId;
        StartUtc = Utc.Require(startUtc);
        Examples = examples;
    }

    public string GameId { get; }

    public DateTime StartUtc { get; }

    public TrainingExample[] Examples { get; }
}
