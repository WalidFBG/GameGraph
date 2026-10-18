namespace GameGraph.TrainingDataGeneration;

public sealed record TrainingDataManifest(
    string Node,
    string Source,
    DateTime CreatedUtc,
    string[] Features,
    int Examples,
    Dictionary<string, int> Games);
