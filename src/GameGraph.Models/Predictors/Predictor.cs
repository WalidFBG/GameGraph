using GameGraph.Contract.Distributions;

namespace GameGraph.Models.Predictors;

internal sealed class Predictor : IParameterValue
{
    private readonly Link _link;
    private readonly int[] _columns;
    private readonly double _intercept;
    private readonly double[] _coefficients;

    public Predictor(Link link, int[] columns, double intercept, double[] coefficients)
    {
        if (coefficients.Length != columns.Length)
            throw new ArgumentException($"{columns.Length} columns need {columns.Length} coefficients, got {coefficients.Length}", nameof(coefficients));

        _link = link;
        _columns = columns;
        _intercept = intercept;
        _coefficients = coefficients;
    }

    public double Value(ReadOnlySpan<double> features, ReadOnlySpan<double> design)
    {
        var eta = _intercept;

        for (var c = 0; c < _coefficients.Length; c++)
            eta += _coefficients[c] * design[_columns[c]];

        return Links.Inverse(_link, eta);
    }
}
