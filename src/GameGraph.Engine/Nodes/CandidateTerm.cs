using GameGraph.Contract.Terms;

namespace GameGraph.Engine.Nodes;

public sealed class CandidateTerm<TState>
{
    internal CandidateTerm(CandidateFeature<TState> feature, Term term)
    {
        Feature = feature;
        Term = term;
    }

    public CandidateFeature<TState> Feature { get; }

    public Term Term { get; }
}

public static class CandidateTerm
{
    public static CandidateTerm<TState> Linear<TState>(CandidateFeature<TState> feature) => new(feature, new LinearTerm(feature.Name));

    public static CandidateTerm<TState> Spline<TState>(CandidateFeature<TState> feature, int knots) => new(feature, new LinearSplineTerm(feature.Name, knots));
}
