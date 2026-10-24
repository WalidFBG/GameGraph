using GameGraph.Models.Contract;

namespace GameGraph.Models;

public sealed class Model
{
    private readonly Predictor[] _predictors;

    private Model(Predictor[] predictors)
    {
        _predictors = predictors;
    }

    public double[] ParameterValues(double[] features)
    {
        var values = new double[_predictors.Length];

        for (var p = 0; p < values.Length; p++)
            values[p] = _predictors[p].Value(features);

        return values;
    }

    public double LogProbability(double[] features, int outcome) => Bernoulli.LogProbability(ParameterValues(features), outcome);

    public int Sample(double[] features, double uniform) => Bernoulli.Sample(ParameterValues(features), uniform);

    public static Model FromFitted(FittedModel fitted, IReadOnlyList<string> features)
    {
        var names = fitted.Distribution switch
        {
            Distribution.Bernoulli => Bernoulli.ParameterNames,
            _ => throw new ArgumentOutOfRangeException(nameof(fitted), fitted.Distribution, "Not a distribution"),
        };

        if (!fitted.Parameters.Keys.Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)))
            throw new ArgumentException($"{fitted.Distribution} has parameters {string.Join(", ", names)}, got {string.Join(", ", fitted.Parameters.Keys)}", nameof(fitted));

        return new Model(names.Select(n => Predictor.FromFitted(fitted.Parameters[n], features)).ToArray());
    }
}
