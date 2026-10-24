using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal sealed class Predictor : IParameterValue
{
    private readonly Link _link;
    private readonly ITermColumns[] _terms;
    private readonly double _intercept;
    private readonly double[] _coefficients;
    private readonly int _widestTerm;

    public Predictor(Link link, ITermColumns[] terms, double intercept, double[] coefficients)
    {
        var columns = terms.Sum(t => t.Count);

        if (coefficients.Length != columns)
            throw new ArgumentException($"{columns} columns need {columns} coefficients, got {coefficients.Length}", nameof(coefficients));

        _link = link;
        _terms = terms;
        _intercept = intercept;
        _coefficients = coefficients;
        _widestTerm = terms.Length == 0 ? 0 : terms.Max(t => t.Count);
    }

    public double Value(ReadOnlySpan<double> features)
    {
        Span<double> columns = stackalloc double[_widestTerm];
        var eta = _intercept;
        var coefficient = 0;

        foreach (var term in _terms)
        {
            term.Expand(features, columns);

            for (var c = 0; c < term.Count; c++)
                eta += _coefficients[coefficient++] * columns[c];
        }

        return Links.Inverse(_link, eta);
    }

    public static Predictor FromFitted(Link link, FittedPredictor predictor, IReadOnlyList<string> features) =>
        new(
            link,
            predictor.Terms.Select(t => TermColumns.FromFitted(t, features)).ToArray(),
            predictor.Intercept,
            predictor.Terms.SelectMany(t => t.Coefficients).ToArray());
}
