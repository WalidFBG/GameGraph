namespace GameGraph.ModelFamilies;

public sealed record TermDescription(string Feature, string Basis)
{
    public ITerm Create() => Basis switch
    {
        LinearTerm.Name => new LinearTerm(Feature),
        _ => throw new InvalidOperationException($"Unknown basis {Basis} for {Feature}"),
    };
}
