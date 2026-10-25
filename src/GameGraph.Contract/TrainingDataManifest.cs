namespace GameGraph.Contract;

public sealed record TrainingDataManifest
{
    public TrainingDataManifest(string node, string source, DateTime createdUtc, string[] features, int examples, ModelDeclaration model)
    {
        DistinctNames.Require(features);

        model.RequireFeatures(node, features);

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
