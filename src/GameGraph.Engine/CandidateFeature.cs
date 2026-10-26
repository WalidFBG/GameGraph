using System.Linq.Expressions;

namespace GameGraph.Engine;

public sealed record CandidateFeature<TState>(string Name, Expression<Func<TState, int, double>> Definition)
{
    public Func<TState, int, double> Compute { get; } = Definition.Compile();

    public Expression Inline(Expression state, Expression candidate) => ParameterSubstitution.Inline(Definition, state, candidate);
}
