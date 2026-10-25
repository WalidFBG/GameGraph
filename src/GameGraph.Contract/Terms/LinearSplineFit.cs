namespace GameGraph.Contract.Terms;

public sealed record LinearSplineFit : TermFit
{
    public LinearSplineFit(double[] knotPositions, double[] centres, double[] scales, double[] coefficients)
        : base(coefficients)
    {
        if (knotPositions.Length < 2)
            throw new ArgumentException($"A linear spline needs at least 2 knot positions, got {string.Join(", ", knotPositions)}", nameof(knotPositions));

        if (knotPositions.Zip(knotPositions.Skip(1)).Any(p => p.First >= p.Second))
            throw new ArgumentException($"The knots {string.Join(", ", knotPositions)} are not strictly increasing", nameof(knotPositions));

        var segments = knotPositions.Length - 1;

        if (centres.Length != segments || scales.Length != segments || coefficients.Length != segments)
            throw new ArgumentException($"A linear spline with {knotPositions.Length} knots needs {segments} centres, scales and coefficients", nameof(coefficients));

        KnotPositions = knotPositions;
        Centres = centres;
        Scales = scales;
    }

    public double[] KnotPositions { get; }

    public double[] Centres { get; }

    public double[] Scales { get; }
}
