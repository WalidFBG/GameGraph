namespace GameGraph.Models.Contract;

public sealed record CategoricalDistribution : DistributionDeclaration
{
    public CategoricalDistribution(string[] categories)
    {
        if (categories.Length < 2)
            throw new ArgumentException($"A categorical distribution needs at least 2 categories, got {string.Join(", ", categories)}", nameof(categories));

        DistinctNames.Require(categories);

        Categories = categories;
    }

    public string[] Categories { get; }

    public override PredictedParameter[] Predicted() => Categories[1..].Select(c => new PredictedParameter(c, Link.Identity)).ToArray();

    public override string[] Given() => [];

    public bool Equals(CategoricalDistribution? other) => other is not null && Categories.SequenceEqual(other.Categories);

    public override int GetHashCode() => Categories.Aggregate(0, (hash, c) => HashCode.Combine(hash, c));
}
