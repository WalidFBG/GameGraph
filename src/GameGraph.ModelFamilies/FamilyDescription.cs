namespace GameGraph.ModelFamilies;

public sealed record FamilyDescription(string Name, int? Categories)
{
    public IModelFamily Create() => Name switch
    {
        nameof(Bernoulli) => new Bernoulli(),
        nameof(Categorical) => new Categorical(Categories
            ?? throw new InvalidOperationException("A categorical family needs its number of categories")),
        _ => throw new InvalidOperationException($"Unknown model family {Name}"),
    };
}
