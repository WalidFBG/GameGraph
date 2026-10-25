using GameGraph.Contract.Terms;

namespace GameGraph.Models.Predictors;

internal static class TermColumns
{
    public static ITermColumns FromFit(string feature, TermFit fit, IReadOnlyList<string> features) => fit switch
    {
        LinearFit linear => LinearColumns.FromFit(feature, linear, features),
        LinearSplineFit spline => LinearSplineColumns.FromFit(feature, spline, features),
        _ => throw new ArgumentOutOfRangeException(nameof(fit), fit.GetType().Name, "Not a term fit"),
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
