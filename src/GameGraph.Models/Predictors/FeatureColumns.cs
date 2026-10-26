namespace GameGraph.Models.Predictors;

internal static class FeatureColumns
{
    public static int Of(string feature, IReadOnlyList<string> features)
    {
        for (var c = 0; c < features.Count; c++)
        {
            if (features[c] == feature)
                return c;
        }

        throw new ArgumentException($"No feature {feature} among {string.Join(", ", features)}", nameof(features));
    }
}
