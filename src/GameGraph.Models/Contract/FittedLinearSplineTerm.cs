namespace GameGraph.Models.Contract;

public sealed record FittedLinearSplineTerm : FittedTerm
{
    public FittedLinearSplineTerm(string feature, int knots, double[] knotPositions, double[] centres, double[] scales, double[] coefficients)
        : base(feature, coefficients)
    {
        if (knotPositions.Length < 2 || knotPositions.Length > knots)
            throw new ArgumentException($"A linear spline of {feature} with {knots} knots has {knotPositions.Length} positions", nameof(knotPositions));

        if (knotPositions.Zip(knotPositions.Skip(1)).Any(p => p.First >= p.Second))
            throw new ArgumentException($"The knots of {feature} are not strictly increasing: {string.Join(", ", knotPositions)}", nameof(knotPositions));

        var segments = knotPositions.Length - 1;

        if (centres.Length != segments || scales.Length != segments || coefficients.Length != segments)
            throw new ArgumentException($"A linear spline of {feature} needs {segments} centres, scales and coefficients", nameof(coefficients));

        Knots = knots;
        KnotPositions = knotPositions;
        Centres = centres;
        Scales = scales;
    }

    public int Knots { get; }

    public double[] KnotPositions { get; }

    public double[] Centres { get; }

    public double[] Scales { get; }

    public override Term Declaration() => new LinearSplineTerm(Feature, Knots);
}
