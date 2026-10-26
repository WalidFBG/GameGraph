using System.Linq.Expressions;
using GameGraph.Contract.Distributions;
using GameGraph.Engine.Nodes;
using GameGraph.Models;

namespace GameGraph.Simulation.Compiled;

public static class NodeCompiler<TState>
{
    public static Expression<Func<TState, double[], int>> Parameters(Node<TState> node, Model model)
    {
        var state = Expression.Parameter(typeof(TState), "state");
        var parameters = Expression.Parameter(typeof(double[]), "parameters");

        return Expression.Lambda<Func<TState, double[], int>>(Written(node, model, state, parameters), $"{node.Name}Parameters", [state, parameters]);
    }

    public static Expression<Func<TState, double[], double, int>> Decision(Node<TState> node, Model model)
    {
        var state = Expression.Parameter(typeof(TState), "state");
        var parameters = Expression.Parameter(typeof(double[]), "parameters");
        var uniform = Expression.Parameter(typeof(double), "uniform");
        var count = Expression.Variable(typeof(int), "count");

        var sampled = Expression.Block(
            [count],
            Expression.Assign(count, Written(node, model, state, parameters)),
            model.SampleExpression(parameters, count, uniform));

        return Expression.Lambda<Func<TState, double[], double, int>>(sampled, node.Name, [state, parameters, uniform]);
    }

    public static int Capacity(Node<TState> node, Model model) =>
        node is ChoiceNode<TState> ? ChoiceShape.MostCandidates * model.ParametersPerRow : model.ParametersPerRow;

    private static Expression Written(Node<TState> node, Model model, ParameterExpression state, ParameterExpression parameters) => node switch
    {
        SingleRowNode<TState> singleRow => SingleRowParameters(singleRow, model, state, parameters),
        ChoiceNode<TState> choice => ChoiceParameters(choice, model, state, parameters),
        _ => throw new NotSupportedException($"{node.Name} is a {node.GetType().Name}, which the compiler does not know"),
    };

    private static Expression SingleRowParameters(SingleRowNode<TState> node, Model model, ParameterExpression state, ParameterExpression parameters)
    {
        var features = node.Features.Select(f => Expression.Variable(typeof(double), f.Name)).ToArray();

        return Expression.Block(
            features,
            [
                .. node.Features.Select((f, i) => Expression.Assign(features[i], f.Inline(state))),
                model.WriteParametersExpression(features, parameters, Expression.Constant(0)),
                Expression.Constant(model.ParametersPerRow),
            ]);
    }

    private static Expression ChoiceParameters(ChoiceNode<TState> node, Model model, ParameterExpression state, ParameterExpression parameters)
    {
        var candidates = Expression.Variable(typeof(int[]), "candidates");
        var size = Expression.Variable(typeof(double), node.Size.Name);
        var row = Expression.Variable(typeof(int), "row");
        var candidate = Expression.Variable(typeof(int), "candidate");
        var features = node.Features.Select(f => Expression.Variable(typeof(double), f.Name)).ToArray();
        var perRow = Expression.Constant(model.ParametersPerRow);
        var count = Expression.ArrayLength(candidates);
        var end = Expression.Label("end");

        var eachCandidate = Expression.Loop(
            Expression.IfThenElse(
                Expression.LessThan(row, count),
                Expression.Block(
                    [candidate, .. features],
                    [
                        Expression.Assign(candidate, Expression.ArrayIndex(candidates, row)),
                        .. node.Features.Select((f, i) => Expression.Assign(features[i], f.Inline(state, candidate))),
                        model.WriteParametersExpression([.. features, size], parameters, Expression.Multiply(row, perRow)),
                        Expression.PreIncrementAssign(row),
                    ]),
                Expression.Break(end)),
            end);

        return Expression.Block(
            [candidates, size, row],
            Expression.Assign(candidates, node.CandidatesInline(state)),
            Expression.Assign(size, node.Size.Inline(state)),
            ChoiceShape.OfExpression(node.Name, count, size),
            Expression.Assign(row, Expression.Constant(0)),
            eachCandidate,
            Expression.Multiply(count, perRow));
    }
}
