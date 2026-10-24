using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal sealed class Predictor
{
    private readonly Link _link;
    private readonly LinearColumns[] _terms;
    private readonly double _intercept;
    private readonly double[] _coefficients;

    public Predictor(Link link, LinearColumns[] terms, double intercept, double[] coefficients)
    {
        if (coefficients.Length != terms.Length)
            throw new ArgumentException($"{terms.Length} linear terms need {terms.Length} coefficients, got {coefficients.Length}", nameof(coefficients));

        _link = link;
        _terms = terms;
        _intercept = intercept;
        _coefficients = coefficients;
    }

    public double Value(ReadOnlySpan<double> features)
    {
        var eta = _intercept;

        for (var t = 0; t < _terms.Length; t++)
            eta += _coefficients[t] * _terms[t].Expand(features);

        return Links.Inverse(_link, eta);
    }

    public static Predictor FromFitted(FittedPredictedParameter parameter, IReadOnlyList<string> features) =>
        new(
            parameter.Link,
            parameter.Terms.Select(t => LinearColumns.FromFitted(t, features)).ToArray(),
            parameter.Intercept,
            parameter.Terms.SelectMany(t => t.Coefficients).ToArray());
}
