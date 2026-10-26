using System.Linq.Expressions;

namespace GameGraph.Models.Predictors;

internal sealed record KnotLookup(int Column, double[] Knots)
{
    public void Locate(ReadOnlySpan<double> features, out int segment, out double value)
    {
        value = Math.Min(Math.Max(features[Column], Knots[0]), Knots[^1]);
        segment = 0;

        while (segment < Knots.Length - 2 && value >= Knots[segment + 1])
            segment++;
    }

    public Expression LocateExpression(IReadOnlyList<Expression> features, ParameterExpression segment, ParameterExpression value)
    {
        var steps = new List<Expression>
        {
            Expression.Assign(value, Expression.Call(MathMethods.Min, Expression.Call(MathMethods.Max, features[Column], Expression.Constant(Knots[0])), Expression.Constant(Knots[^1]))),
            Expression.Assign(segment, Expression.Constant(0)),
        };

        for (var next = 1; next < Knots.Length - 1; next++)
        {
            steps.Add(Expression.IfThen(
                Expression.AndAlso(Expression.Equal(segment, Expression.Constant(next - 1)), Expression.GreaterThanOrEqual(value, Expression.Constant(Knots[next]))),
                Expression.Assign(segment, Expression.Constant(next))));
        }

        return Expression.Block(steps);
    }

    public static int IndexIn(List<KnotLookup> lookups, int column, double[] knots)
    {
        var index = lookups.FindIndex(l => l.Column == column && l.Knots.SequenceEqual(knots));

        if (index >= 0)
            return index;

        lookups.Add(new KnotLookup(column, knots));

        return lookups.Count - 1;
    }
}
