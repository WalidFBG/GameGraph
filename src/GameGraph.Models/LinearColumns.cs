using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal sealed record LinearColumns(int Column, double Centre, double Scale)
{
    public double Expand(ReadOnlySpan<double> features) => (features[Column] - Centre) / Scale;

    public static LinearColumns FromFitted(FittedLinearTerm term, IReadOnlyList<string> features)
    {
        var column = features.ToList().IndexOf(term.Feature);

        if (column < 0)
            throw new ArgumentException($"No feature {term.Feature} among {string.Join(", ", features)}", nameof(features));

        return new LinearColumns(column, term.Centre, term.Scale);
    }
}
