using System.Linq.Expressions;

namespace GameGraph.Engine;

public sealed record Feature<TState>(string Name, Expression<Func<TState, double>> Definition)
{
    public Func<TState, double> Compute { get; } = Definition.Compile();

    public Expression Inline(Expression state) => ParameterSubstitution.Inline(Definition, state);
}
