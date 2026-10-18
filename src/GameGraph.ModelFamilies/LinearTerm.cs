namespace GameGraph.ModelFamilies;

public sealed record LinearTerm(string Feature) : ITerm
{
    public const string Name = "Linear";

    public string Basis => Name;

    public int Columns => 1;

    public void Expand(double value, Span<double> columns) => columns[0] = value;

    public TermDescription Describe() => new(Feature, Basis);
}
