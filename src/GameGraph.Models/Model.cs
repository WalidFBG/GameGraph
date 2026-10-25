using GameGraph.Contract;
using GameGraph.Contract.Distributions;
using GameGraph.Models.Distributions;
using GameGraph.Models.Predictors;

namespace GameGraph.Models;

public sealed class Model
{
    private readonly IDistribution _distribution;
    private readonly IParameterValue[] _parameters;
    private readonly int _features;
    private readonly bool _choosesAmongCandidates;

    private Model(IDistribution distribution, IParameterValue[] parameters, int features, bool choosesAmongCandidates)
    {
        if (choosesAmongCandidates && features == 0)
            throw new ArgumentException("A choice needs a feature to tell its candidates apart", nameof(features));

        _distribution = distribution;
        _parameters = parameters;
        _features = features;
        _choosesAmongCandidates = choosesAmongCandidates;
    }

    public void ParameterValues(ReadOnlySpan<double> features, Span<double> values)
    {
        var rows = Rows(features);

        if (values.Length != rows * _parameters.Length)
            throw new ArgumentException($"Expected {rows * _parameters.Length} parameter values, got {values.Length}", nameof(values));

        for (var r = 0; r < rows; r++)
        {
            var row = features.Slice(r * _features, _features);

            for (var p = 0; p < _parameters.Length; p++)
                values[r * _parameters.Length + p] = _parameters[p].Value(row);
        }
    }

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
        var predictors = model.Distribution.Predicted().Select(p => Predictor.FromFit(p.Link, model.Terms[p.Name], fitted.Fit[p.Name], features));
        var given = model.Distribution.Given().Select(n => new GivenColumn(TermColumns.ColumnOf(model.Given[n], features)));

        return new(DistributionFor(model.Distribution), [.. predictors, .. given], features.Count, model.Distribution.ChoosesAmongCandidates());
    }

    private static IDistribution DistributionFor(DistributionDeclaration declaration) => declaration switch
    {
        BernoulliDistribution => new Bernoulli(),
        CategoricalDistribution categorical => new Categorical(categorical.Categories.Length),
        DiscreteJohnsonSuDistribution => new DiscreteJohnsonSu(),
        ChoiceDistribution => new Choice(),
        _ => throw new ArgumentOutOfRangeException(nameof(declaration), declaration.GetType().Name, "Not a distribution"),
    };

    private int ParameterValueCount(ReadOnlySpan<double> features) => Rows(features) * _parameters.Length;

    private int Rows(ReadOnlySpan<double> features)
    {
        var rows = _choosesAmongCandidates ? features.Length / _features : 1;

        if (features.Length != rows * _features || rows == 0)
            throw new ArgumentException($"{features.Length} feature values are not {(_choosesAmongCandidates ? "one or more candidates" : "one row")} of {_features} features", nameof(features));

        return rows;
    }
}
