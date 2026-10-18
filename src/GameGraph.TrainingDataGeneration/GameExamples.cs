namespace GameGraph.TrainingDataGeneration;

public sealed record GameExamples(string GameId, DateTime StartUtc, List<TrainingExample> Examples);
