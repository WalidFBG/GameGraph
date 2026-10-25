namespace GameGraph.Contract;

public sealed record GameExamples(string GameId, DateTime StartUtc, TrainingExample[] Examples);
