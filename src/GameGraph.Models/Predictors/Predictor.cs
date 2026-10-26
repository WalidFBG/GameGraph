using System.Linq.Expressions;
using GameGraph.Contract.Distributions;
using GameGraph.Contract.Terms;
using GameGraph.Contract;

namespace GameGraph.Models.Predictors;

internal sealed class Predictor
{
    private readonly Link _link;
    private readonly double _intercept;
    private readonly int[] _linearColumns;
    private readonly double[] _linearSlopes;
    private readonly int[] _splineLookups;
    private readonly double[][] _splineIntercepts;
    private readonly double[][] _splineSlopes;

    private Predictor(Link link, double intercept, int[] linearColumns, double[] linearSlopes, int[] splineLookups, double[][] splineIntercepts, double[][] splineSlopes)
    {
        _link = link;
        _intercept = intercept;
        _linearColumns = linearColumns;
        _linearSlopes = linearSlopes;
        _splineLookups = splineLookups;
        _splineIntercepts = splineIntercepts;
        _splineSlopes = splineSlopes;
    }

    public double Value(ReadOnlySpan<double> features, ReadOnlySpan<int> segments, ReadOnlySpan<double> located)
    {
        var eta = _intercept;

        for (var t = 0; t < _linearColumns.Length; t++)
            eta += _linearSlopes[t] * features[_linearColumns[t]];

        for (var t = 0; t < _splineLookups.Length; t++)
        {
            var lookup = _splineLookups[t];
            var segment = segments[lookup];

            eta += _splineIntercepts[t][segment] + _splineSlopes[t][segment] * located[lookup];
        }

        return Links.Inverse(_link, eta);
    }

    public Expression ValueExpression(IReadOnlyList<Expression> features, IReadOnlyList<ParameterExpression> segments, IReadOnlyList<ParameterExpression> located)
    {
        Expression eta = Expression.Constant(_intercept);

        for (var t = 0; t < _linearColumns.Length; t++)
            eta = Expression.Add(eta, Expression.Multiply(Expression.Constant(_linearSlopes[t]), features[_linearColumns[t]]));

        for (var t = 0; t < _splineLookups.Length; t++)
        {
            var lookup = _splineLookups[t];
            var segment = segments[lookup];

            eta = Expression.Add(
                eta,
                Expression.Add(
                    Expression.ArrayIndex(Expression.Constant(_splineIntercepts[t]), segment),
                    Expression.Multiply(Expression.ArrayIndex(Expression.Constant(_splineSlopes[t]), segment), located[lookup])));
        }

        return Expression.Call(Links.InverseMethod, Expression.Constant(_link), eta);
    }

    public static Predictor FromFit(Link link, Term[] terms, PredictorFit fit, IReadOnlyList<string> features, List<KnotLookup> lookups)
    {
        var intercept = fit.Intercept;
        var linearColumns = new List<int>();
        var linearSlopes = new List<double>();
        var splineLookups = new List<int>();
        var splineIntercepts = new List<double[]>();
        var splineSlopes = new List<double[]>();

        foreach (var term in terms)
        {
            var column = FeatureColumns.Of(term.Feature, features);

            switch (fit.Terms[term.Feature])
            {
                case LinearFit linear:
                    var slope = linear.Coefficients[0] / linear.Scale;
                    intercept -= slope * linear.Centre;
                    linearColumns.Add(column);
                    linearSlopes.Add(slope);
                    break;
                case LinearSplineFit spline:
                    var (pieceIntercepts, pieceSlopes) = Pieces(spline);
                    splineLookups.Add(KnotLookup.IndexIn(lookups, column, spline.KnotPositions));
                    splineIntercepts.Add(pieceIntercepts);
                    splineSlopes.Add(pieceSlopes);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fit), fit.Terms[term.Feature].GetType().Name, "Not a term fit");
            }
        }

        return new(link, intercept, [.. linearColumns], [.. linearSlopes], [.. splineLookups], [.. splineIntercepts], [.. splineSlopes]);
    }

    private static (double[] Intercepts, double[] Slopes) Pieces(LinearSplineFit spline)
    {
        var knots = spline.KnotPositions;
        var segments = knots.Length - 1;
        var intercepts = new double[segments];
        var slopes = new double[segments];

        for (var j = 0; j < segments; j++)
        {
            slopes[j] = spline.Coefficients[j] / spline.Scales[j];

            for (var s = 0; s < segments; s++)
            {
                var offset = s < j ? knots[s + 1] - knots[s] : s == j ? -knots[s] : 0;

                intercepts[j] += spline.Coefficients[s] * (offset - spline.Centres[s]) / spline.Scales[s];
            }
        }

        return (intercepts, slopes);
    }
}
