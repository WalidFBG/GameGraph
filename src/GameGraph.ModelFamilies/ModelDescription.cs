namespace GameGraph.ModelFamilies;

public sealed record ModelDescription(FamilyDescription Family, TermDescription[] Terms)
{
    public Model Create(string[] features) => new(Family.Create(), Terms.Select(t => t.Create()).ToArray(), features);
}
