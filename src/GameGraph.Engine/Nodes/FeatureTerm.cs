using GameGraph.Contract.Terms;

namespace GameGraph.Engine.Nodes;

public sealed class FeatureTerm<TState>
{
    internal FeatureTerm(Feature<TState> feature, Term term)
    {
        Feature = feature;
        Term = term;
    }

    public Feature<TState> Feature { get; }

    public Term Term { get; }
}

public static class FeatureTerm
{
    public static FeatureTerm<TState> Linear<TState>(Feature<TState> feature) => new(feature, new LinearTerm(feature.Name));

    public static FeatureTerm<TState> Spline<TState>(Feature<TState> feature, int knots) => new(feature, new LinearSplineTerm(feature.Name, knots));
}
