using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal static class TermColumns
{
    public static ITermColumns FromFitted(FittedTerm term, IReadOnlyList<string> features) => term switch
    {
        FittedLinearTerm linear => LinearColumns.FromFitted(linear, features),
        FittedLinearSplineTerm spline => LinearSplineColumns.FromFitted(spline, features),
        _ => throw new ArgumentOutOfRangeException(nameof(term), term.GetType().Name, "Not a term"),
    };

    public static int ColumnOf(string feature, IReadOnlyList<string> features)
    {
        for (var c = 0; c < features.Count; c++)
        {
            if (features[c] == feature)
                return c;
        }

        throw new ArgumentException($"No feature {feature} among {string.Join(", ", features)}", nameof(features));
    }
}
