namespace GameGraph.ModelFamilies;

public interface ITerm
{
    string Feature { get; }

    string Basis { get; }

    int Columns { get; }

    void Expand(double value, Span<double> columns);

    TermDescription Describe();
}
