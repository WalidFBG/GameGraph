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
        var features = manifest.Features.Length;
        var rows = example.Features.Length / Math.Max(features, 1);

        if (rows == 0 || rows > ChoiceDistribution.MostCandidates || example.Features.Length != rows * features)
            throw new ArgumentException($"An example of {manifest.Node} has {example.Features.Length} feature values, not 1 to {ChoiceDistribution.MostCandidates} candidates of {features} features", nameof(example));

        if (example.Outcome <= 0 || example.Outcome >> rows != 0)
            throw new ArgumentException($"An example of {manifest.Node} chooses {example.Outcome}, not a set of its {rows} candidates' rows", nameof(example));

        var size = Array.IndexOf(manifest.Features, manifest.Model.Given[ChoiceDistribution.Size]);

        for (var row = 0; row < rows; row++)
        {
            if (example.Features[row * features + size] != int.PopCount(example.Outcome))
                throw new ArgumentException($"An example of {manifest.Node} chooses {int.PopCount(example.Outcome)} candidates, but its size is {example.Features[row * features + size]}", nameof(example));
        }
    }
}
