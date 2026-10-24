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

    public override string[] ParameterNames() => Categories[1..];
}
