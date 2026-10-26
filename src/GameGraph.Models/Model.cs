using GameGraph.Contract;
using GameGraph.Contract.Distributions;
using GameGraph.Models.Distributions;
using GameGraph.Models.Predictors;

namespace GameGraph.Models;

public sealed class Model
{
    private readonly IDistribution _distribution;
    private readonly DesignColumns _design;
    private readonly IParameterValue[] _parameters;
    private readonly int _features;
    private readonly bool _choosesAmongCandidates;

    private Model(IDistribution distribution, DesignColumns design, IParameterValue[] parameters, int features, bool choosesAmongCandidates)
    {
        if (choosesAmongCandidates && features == 0)
            throw new ArgumentException("A choice needs a feature to tell its candidates apart", nameof(features));

        _distribution = distribution;
        _design = design;
        _parameters = parameters;
        _features = features;
        _choosesAmongCandidates = choosesAmongCandidates;
    }

    public void ParameterValues(ReadOnlySpan<double> features, Span<double> values)
    {
        var rows = Rows(features);

        if (values.Length != rows * _parameters.Length)
            throw new ArgumentException($"Expected {rows * _parameters.Length} parameter values, got {values.Length}", nameof(values));

        Span<double> design = stackalloc double[_design.Count];

        for (var r = 0; r < rows; r++)
        {
            var row = features.Slice(r * _features, _features);

            _design.Expand(row, design);

            for (var p = 0; p < _parameters.Length; p++)
                values[r * _parameters.Length + p] = _parameters[p].Value(row, design);
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
        var predicted = model.Distribution.Predicted()
            .Select(p => (p.Link, Fit: fitted.Fit[p.Name], Terms: model.Terms[p.Name].Select(t => TermColumns.FromFit(t.Feature, fitted.Fit[p.Name].Terms[t.Feature], features)).ToArray(), Declared: model.Terms[p.Name]))
            .ToArray();
        var design = new DesignColumns([.. predicted.SelectMany(p => p.Terms).Distinct()]);
        var predictors = predicted.Select(p => new Predictor(p.Link, [.. p.Terms.SelectMany(design.ColumnsOf)], p.Fit.Intercept, [.. p.Declared.SelectMany(t => p.Fit.Terms[t.Feature].Coefficients)]));
        var given = model.Distribution.Given().Select(n => new GivenColumn(TermColumns.ColumnOf(model.Given[n], features)));

        return new(DistributionFor(model.Distribution), design, [.. predictors, .. given], features.Count, model.Distribution.ChoosesAmongCandidates());
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

        if (features.Length != rows * _features || (rows == 0 && !_choosesAmongCandidates))
            throw new ArgumentException($"{features.Length} feature values are not {(_choosesAmongCandidates ? "rows of candidates" : "one row")} of {_features} features", nameof(features));

        return rows;
    }
}
