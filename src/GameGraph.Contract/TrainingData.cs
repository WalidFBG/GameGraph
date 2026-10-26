using GameGraph.Contract.Distributions;

namespace GameGraph.Contract;

public sealed record TrainingData
{
    public TrainingData(TrainingDataManifest manifest, GameExamples[] games)
    {
        var examples = games.Sum(g => g.Examples.Length);

        if (examples != manifest.Examples)
            throw new ArgumentException($"The manifest of {manifest.Node} says {manifest.Examples} examples, its games hold {examples}", nameof(games));

        foreach (var example in games.SelectMany(g => g.Examples))
        {
            if (manifest.Model.Distribution.ChoosesAmongCandidates())
                RequireChoice(manifest, example);
            else if (example.Features.Length != manifest.Features.Length)
                throw new ArgumentException($"An example of {manifest.Node} has {example.Features.Length} feature values, not one row of {manifest.Features.Length} features", nameof(games));
        }

        Manifest = manifest;
        Games = games;
    }

    public TrainingDataManifest Manifest { get; }

    public GameExamples[] Games { get; }

    public int ExamplesIn(int games) => Games.Take(games).Sum(g => g.Examples.Length);

    private static void RequireChoice(TrainingDataManifest manifest, TrainingExample example)
    {
        var choice = $"An example of {manifest.Node}";
        var features = manifest.Features.Length;
        var rows = example.Features.Length / Math.Max(features, 1);

        if (rows == 0 || example.Features.Length != rows * features)
            throw new ArgumentException($"{choice} has {example.Features.Length} feature values, not rows of {features} features", nameof(example));

        var size = Array.IndexOf(manifest.Features, manifest.Model.Given[ChoiceDistribution.Size]);
        var shape = ChoiceShape.Of(choice, rows, example.Features[size]);

        for (var row = 1; row < rows; row++)
        {
            if (example.Features[row * features + size] != shape.Size)
                throw new ArgumentException($"{choice} has the size {shape.Size} for its first candidate, but {example.Features[row * features + size]} for candidate {row}", nameof(example));
        }

        shape.RequireOutcome(choice, example.Outcome);

        if (shape.Size == 0)
            throw new ArgumentException($"{choice} chooses none of its {shape.Candidates} candidates", nameof(example));
    }
}
