namespace GameGraph.Models.Contract;

public sealed record TrainingDataManifest
{
    public TrainingDataManifest(string node, string source, DateTime createdUtc, string[] features, int examples, ModelDeclaration model)
    {
        DistinctNames.Require(features);

        var missing = model.Features().Except(features).Order(StringComparer.Ordinal).ToArray();

        if (missing.Length > 0)
            throw new ArgumentException($"The model of {node} uses {string.Join(", ", missing)}, which are not among its features", nameof(model));

        Node = node;
        Source = source;
        CreatedUtc = Utc.Require(createdUtc);
        Features = features;
        Examples = examples;
        Model = model;
    }

    public string Node { get; }

    public string Source { get; }

    public DateTime CreatedUtc { get; }

    public string[] Features { get; }

    public int Examples { get; }

    public ModelDeclaration Model { get; }
}
