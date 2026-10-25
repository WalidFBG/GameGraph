using GameGraph.Models.Contract;

namespace GameGraph.Models;

public sealed class Model
{
    private readonly IDistribution _distribution;
    private readonly IParameterValue[] _parameters;

    private Model(IDistribution distribution, IParameterValue[] parameters)
    {
        _distribution = distribution;
        _parameters = parameters;
    }

    public void ParameterValues(ReadOnlySpan<double> features, Span<double> values)
    {
        if (values.Length != _parameters.Length)
            throw new ArgumentException($"Expected {_parameters.Length} parameter values, got {values.Length}", nameof(values));

        for (var p = 0; p < values.Length; p++)
            values[p] = _parameters[p].Value(features);
    }

    public double LogProbability(ReadOnlySpan<double> features, int outcome)
    {
        Span<double> parameters = stackalloc double[_parameters.Length];
        ParameterValues(features, parameters);
        return _distribution.LogProbability(parameters, outcome);
    }

    public int Sample(ReadOnlySpan<double> features, double uniform)
    {
        Span<double> parameters = stackalloc double[_parameters.Length];
        ParameterValues(features, parameters);
        return _distribution.Sample(parameters, uniform);
    }

    public double[] Probabilities(ReadOnlySpan<double> features)
    {
        Span<double> parameters = stackalloc double[_parameters.Length];
        ParameterValues(features, parameters);
        return _distribution.Probabilities(parameters);
    }

    public static Model FromFitted(FittedModel fitted, IReadOnlyList<string> features)
    {
        var model = fitted.Model;
        var predictors = model.Distribution.Predicted().Select(p => Predictor.FromFit(p.Link, model.Terms[p.Name], fitted.Fit[p.Name], features));
        var given = model.Distribution.Given().Select(n => new GivenColumn(TermColumns.ColumnOf(model.Given[n], features)));

        return new(Distributions.For(model.Distribution), [.. predictors, .. given]);
    }
}
