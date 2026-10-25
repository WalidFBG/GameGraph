namespace GameGraph.Contract;

public sealed record TrainingData
{
    public TrainingData(TrainingDataManifest manifest, GameExamples[] games)
    {
        var examples = games.Sum(g => g.Examples.Length);

        if (examples != manifest.Examples)
            throw new ArgumentException($"The manifest of {manifest.Node} says {manifest.Examples} examples, its games hold {examples}", nameof(games));

        var features = manifest.Features.Length;
        var choice = manifest.Model.Distribution.ChoosesAmongCandidates();

        foreach (var example in games.SelectMany(g => g.Examples))
        {
            var rows = choice ? example.Features.Length / Math.Max(features, 1) : 1;

            if (rows == 0 || example.Features.Length != rows * features)
                throw new ArgumentException($"An example of {manifest.Node} has {example.Features.Length} feature values, not {(choice ? "one or more candidates" : "one row")} of {features} features", nameof(games));

            if (choice && (example.Outcome < 0 || example.Outcome >= rows))
                throw new ArgumentException($"An example of {manifest.Node} chooses candidate {example.Outcome} of {rows}", nameof(games));
        }

        Manifest = manifest;
        Games = games;
    }

    public TrainingDataManifest Manifest { get; }

    public GameExamples[] Games { get; }

    public int ExamplesIn(int games) => Games.Take(games).Sum(g => g.Examples.Length);
}
