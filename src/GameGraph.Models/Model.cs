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

    public double[] ParameterValues(double[] features)
    {
        var values = new double[_parameters.Length];

        for (var p = 0; p < values.Length; p++)
            values[p] = _parameters[p].Value(features);

        return values;
    }

    public double LogProbability(double[] features, int outcome) => _distribution.LogProbability(ParameterValues(features), outcome);

    public int Sample(double[] features, double uniform) => _distribution.Sample(ParameterValues(features), uniform);

    public static Model FromFitted(FittedModel fitted, IReadOnlyList<string> features) =>
        new(
            Distributions.For(fitted.Distribution),
            fitted.Distribution.ParameterNames().Select(n => Parameters.FromFitted(fitted.Parameters[n], features)).ToArray());
}
