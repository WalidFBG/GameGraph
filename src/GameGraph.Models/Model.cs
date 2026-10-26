using System.Linq.Expressions;
using GameGraph.Contract;
using GameGraph.Contract.Distributions;
using GameGraph.Models.Distributions;
using GameGraph.Models.Predictors;

namespace GameGraph.Models;

public sealed class Model
{
    private readonly IDistribution _distribution;
    private readonly KnotLookup[] _lookups;
    private readonly Predictor[] _predictors;
    private readonly int[] _givenColumns;
    private readonly int _features;
    private readonly bool _choosesAmongCandidates;

    private Model(IDistribution distribution, KnotLookup[] lookups, Predictor[] predictors, int[] givenColumns, int features, bool choosesAmongCandidates)
    {
        if (choosesAmongCandidates && features == 0)
            throw new ArgumentException("A choice needs a feature to tell its candidates apart", nameof(features));

        _distribution = distribution;
        _lookups = lookups;
        _predictors = predictors;
        _givenColumns = givenColumns;
        _features = features;
        _choosesAmongCandidates = choosesAmongCandidates;
    }

    public void ParameterValues(ReadOnlySpan<double> features, Span<double> values)
    {
        var rows = Rows(features);

        if (values.Length != rows * ParametersPerRow)
            throw new ArgumentException($"Expected {rows * ParametersPerRow} parameter values, got {values.Length}", nameof(values));

        Span<int> segments = stackalloc int[_lookups.Length];
        Span<double> located = stackalloc double[_lookups.Length];

        for (var r = 0; r < rows; r++)
        {
            var row = features.Slice(r * _features, _features);
            var at = r * ParametersPerRow;

            for (var l = 0; l < _lookups.Length; l++)
                _lookups[l].Locate(row, out segments[l], out located[l]);

            for (var p = 0; p < _predictors.Length; p++)
                values[at++] = _predictors[p].Value(row, segments, located);

            for (var g = 0; g < _givenColumns.Length; g++)
                values[at++] = row[_givenColumns[g]];
        }
    }

    internal Expression WriteParametersExpression(IReadOnlyList<Expression> row, Expression parameters, Expression at)
    {
        if (row.Count != _features)
            throw new ArgumentException($"A row has {_features} features, given {row.Count}", nameof(row));

        var segments = _lookups.Select(_ => Expression.Variable(typeof(int))).ToArray();
        var located = _lookups.Select(_ => Expression.Variable(typeof(double))).ToArray();
        var steps = new List<Expression>();
        var offset = 0;

        for (var l = 0; l < _lookups.Length; l++)
            steps.Add(_lookups[l].LocateExpression(row, segments[l], located[l]));

        foreach (var predictor in _predictors)
            steps.Add(Expression.Assign(Expression.ArrayAccess(parameters, Expression.Add(at, Expression.Constant(offset++))), predictor.ValueExpression(row, segments, located)));

        foreach (var column in _givenColumns)
            steps.Add(Expression.Assign(Expression.ArrayAccess(parameters, Expression.Add(at, Expression.Constant(offset++))), row[column]));

        steps.Add(Expression.Empty());

        return Expression.Block([.. segments, .. located], steps);
    }

    internal int SampleFrom(double[] parameters, int count, double uniform) => _distribution.Sample(parameters.AsSpan(0, count), uniform);

    public double LogProbability(ReadOnlySpan<double> features, int outcome)
    {
        Span<double> parameters = stackalloc double[ParameterValueCount(features)];
        ParameterValues(features, parameters);
        return _distribution.LogProbability(parameters, outcome);
    }

    public int Sample(ReadOnlySpan<double> features, double uniform)
    {
        Span<double> parameters = stackalloc double[ParameterValueCount(features)];
        ParameterValues(features, parameters);
        return _distribution.Sample(parameters, uniform);
    }

    public double[] Probabilities(ReadOnlySpan<double> features)
    {
        Span<double> parameters = stackalloc double[ParameterValueCount(features)];
        ParameterValues(features, parameters);
        return _distribution.Probabilities(parameters);
    }

    public static Model FromFitted(FittedModel fitted, IReadOnlyList<string> features)
    {
        var model = fitted.Model;
        var lookups = new List<KnotLookup>();
        var predictors = model.Distribution.Predicted().Select(p => Predictor.FromFit(p.Link, model.Terms[p.Name], fitted.Fit[p.Name], features, lookups)).ToArray();
        var given = model.Distribution.Given().Select(n => FeatureColumns.Of(model.Given[n], features)).ToArray();

        return new(DistributionFor(model.Distribution), [.. lookups], predictors, given, features.Count, model.Distribution.ChoosesAmongCandidates());
    }

    private static IDistribution DistributionFor(DistributionDeclaration declaration) => declaration switch
    {
        BernoulliDistribution => new Bernoulli(),
        CategoricalDistribution categorical => new Categorical(categorical.Categories.Length),
        DiscreteJohnsonSuDistribution => new DiscreteJohnsonSu(),
        ChoiceDistribution => new Choice(),
        _ => throw new ArgumentOutOfRangeException(nameof(declaration), declaration.GetType().Name, "Not a distribution"),
    };

    internal int ParametersPerRow => _predictors.Length + _givenColumns.Length;

    private int ParameterValueCount(ReadOnlySpan<double> features) => Rows(features) * ParametersPerRow;

    private int Rows(ReadOnlySpan<double> features)
    {
        var rows = _choosesAmongCandidates ? features.Length / _features : 1;

        if (features.Length != rows * _features || (rows == 0 && !_choosesAmongCandidates))
            throw new ArgumentException($"{features.Length} feature values are not {(_choosesAmongCandidates ? "rows of candidates" : "one row")} of {_features} features", nameof(features));

        return rows;
    }
}
