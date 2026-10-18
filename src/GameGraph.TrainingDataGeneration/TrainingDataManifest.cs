using GameGraph.ModelFamilies;

namespace GameGraph.TrainingDataGeneration;

public sealed record TrainingDataManifest(
    string Node,
    ModelDescription Model,
    string Source,
    DateTime CreatedUtc,
    string[] Features,
    int Examples,
    Dictionary<string, int> Tally);
