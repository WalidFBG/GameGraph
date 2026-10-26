using System.Linq.Expressions;

namespace GameGraph.Engine.Nodes;

internal sealed class ParameterSubstitution(IReadOnlyList<ParameterExpression> parameters, IReadOnlyList<Expression> arguments) : ExpressionVisitor
{
    public static Expression Inline(LambdaExpression definition, params Expression[] arguments) =>
        definition.Parameters.Count == arguments.Length
            ? new ParameterSubstitution(definition.Parameters, arguments).Visit(definition.Body)
            : throw new ArgumentException($"{definition} takes {definition.Parameters.Count} arguments, given {arguments.Length}", nameof(arguments));

    protected override Expression VisitParameter(ParameterExpression node)
    {
        for (var p = 0; p < parameters.Count; p++)
        {
            if (parameters[p] == node)
                return arguments[p];
        }

        return node;
    }
}
