using System.Linq.Expressions;
using System.Reflection;
using GameGraph.Contract.Distributions;
using GameGraph.Engine;
using GameGraph.Models;

namespace GameGraph.Simulation;

public sealed class CompiledGame<TState>
{
    private static readonly MethodInfo SampleFrom = typeof(Model).GetMethod(nameof(Model.SampleFrom), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo RequireSize = typeof(ChoiceNode<TState>).GetMethod(nameof(ChoiceNode<TState>.RequireSize))!;
    private static readonly MethodInfo RequireCandidates = typeof(CompiledGame<TState>).GetMethod(nameof(RequireCandidatesWithinCapacity), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly Expression<Func<TState, double[], int>>[] _parameters;
    private readonly Func<TState, double[], double, int>[] _nodes;

    public CompiledGame(FittedGame<TState> fitted)
    {
        Spec = fitted.Spec;

        var compiled = Spec.Nodes.Select(n => Compile(n, fitted.ModelOf(n))).ToArray();

        _parameters = [.. compiled.Select(c => c.Parameters)];
        _nodes = [.. compiled.Select(c => c.Node.Compile())];
        ParameterCapacity = Spec.Nodes.Max(n => Capacity(n, fitted.ModelOf(n)));
    }

    public GameSpec<TState> Spec { get; }

    public int ParameterCapacity { get; }

    public int Decide(NodeSpec<TState> node, TState state, double[] parameters, double uniform) => _nodes[Spec.IndexOf(node)](state, parameters, uniform);

    public Func<TState, double[], int> ParametersOf(NodeSpec<TState> node) => _parameters[Spec.IndexOf(node)].Compile();

    private static (Expression<Func<TState, double[], int>> Parameters, Expression<Func<TState, double[], double, int>> Node) Compile(NodeSpec<TState> node, Model model)
    {
        var state = Expression.Parameter(typeof(TState), "state");
        var parameters = Expression.Parameter(typeof(double[]), "parameters");
        var uniform = Expression.Parameter(typeof(double), "uniform");
        var count = Expression.Variable(typeof(int), "count");

        var written = node switch
        {
            VectorNode<TState> vector => VectorParameters(vector, model, state, parameters),
            ChoiceNode<TState> choice => ChoiceParameters(choice, model, state, parameters),
            _ => throw new NotSupportedException($"{node.Name} is a {node.GetType().Name}, which the compiler does not know"),
        };

        var sampled = Expression.Block(
            [count],
            Expression.Assign(count, written),
            Expression.Call(Expression.Constant(model), SampleFrom, parameters, count, uniform));

        return (
            Expression.Lambda<Func<TState, double[], int>>(written, $"{node.Name}Parameters", [state, parameters]),
            Expression.Lambda<Func<TState, double[], double, int>>(sampled, node.Name, [state, parameters, uniform]));
    }

    private static Expression VectorParameters(VectorNode<TState> node, Model model, ParameterExpression state, ParameterExpression parameters)
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
            Expression.Call(Expression.Constant(node), RequireSize, size, count),
            Expression.Call(RequireCandidates, Expression.Constant(node.Name), count),
            Expression.Assign(row, Expression.Constant(0)),
            eachCandidate,
            Expression.Multiply(count, perRow));
    }

    private static int Capacity(NodeSpec<TState> node, Model model) =>
        node is ChoiceNode<TState> ? ChoiceDistribution.MostCandidates * model.ParametersPerRow : model.ParametersPerRow;

    private static void RequireCandidatesWithinCapacity(string node, int candidates)
    {
        if (candidates > ChoiceDistribution.MostCandidates)
            throw new InvalidOperationException($"{node} has {candidates} candidates, more than a choice's {ChoiceDistribution.MostCandidates}");
    }
}
