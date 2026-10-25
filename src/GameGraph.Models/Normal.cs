namespace GameGraph.Models;

internal static class Normal
{
    private const double Threshold = 0.46875;
    private const double ReciprocalSquareRootOfPi = 5.6418958354775628695e-1;
    private const double Small = 1.11e-16;
    private const double Big = 26.543;
    private const double Huge = 6.71e7;
    private const double Largest = 2.53e307;
    private const double LowestScaled = -26.628;

    private static readonly double[] A = [3.16112374387056560e00, 1.13864154151050156e02, 3.77485237685302021e02, 3.20937758913846947e03, 1.85777706184603153e-1];
    private static readonly double[] B = [2.36012909523441209e01, 2.44024637934444173e02, 1.28261652607737228e03, 2.84423683343917062e03];
    private static readonly double[] C =
    [
        5.64188496988670089e-1, 8.88314979438837594e00, 6.61191906371416295e01, 2.98635138197400131e02,
        8.81952221241769090e02, 1.71204761263407058e03, 2.05107837782607147e03, 1.23033935479799725e03, 2.15311535474403846e-8,
    ];
    private static readonly double[] D =
    [
        1.57449261107098347e01, 1.17693950891312499e02, 5.37181101862009858e02, 1.62138957456669019e03,
        3.29079923573345963e03, 4.36261909014324716e03, 3.43936767414372164e03, 1.23033935480374942e03,
    ];
    private static readonly double[] P = [3.05326634961232344e-1, 3.60344899949804439e-1, 1.25781726111229246e-1, 1.60837851487422766e-2, 6.58749161529837803e-4, 1.63153871373020978e-2];
    private static readonly double[] Q = [2.56852019228982242e00, 1.87295284992346725e00, 5.27905102951428412e-1, 6.05183413124413191e-2, 2.33520497626869185e-3];

    private static readonly double[] CentralNumerator =
    [
        3.3871328727963666080e0, 1.3314166789178437745e+2, 1.9715909503065514427e+3, 1.3731693765509461125e+4,
        4.5921953931549871457e+4, 6.7265770927008700853e+4, 3.3430575583588128105e+4, 2.5090809287301226727e+3,
    ];
    private static readonly double[] CentralDenominator =
    [
        1.0, 4.2313330701600911252e+1, 6.8718700749205790830e+2, 5.3941960214247511077e+3,
        2.1213794301586595867e+4, 3.9307895800092710610e+4, 2.8729085735721942674e+4, 5.2264952788528545610e+3,
    ];
    private static readonly double[] NearNumerator =
    [
        1.42343711074968357734e0, 4.63033784615654529590e0, 5.76949722146069140550e0, 3.64784832476320460504e0,
        1.27045825245236838258e0, 2.41780725177450611770e-1, 2.27238449892691845833e-2, 7.74545014278341407640e-4,
    ];
    private static readonly double[] NearDenominator =
    [
        1.0, 2.05319162663775882187e0, 1.67638483018380384940e0, 6.89767334985100004550e-1,
        1.48103976427480074590e-1, 1.51986665636164571966e-2, 5.47593808499534494600e-4, 1.05075007164441684324e-9,
    ];
    private static readonly double[] FarNumerator =
    [
        6.65790464350110377720e0, 5.46378491116411436990e0, 1.78482653991729133580e0, 2.96560571828504891230e-1,
        2.65321895265761230930e-2, 1.24266094738807843860e-3, 2.71155556874348757815e-5, 2.01033439929228813265e-7,
    ];
    private static readonly double[] FarDenominator =
    [
        1.0, 5.99832206555887937690e-1, 1.36929880922735805310e-1, 1.48753612908506148525e-2,
        7.86869131145613259100e-4, 1.84631831751005468180e-5, 1.42151175831644588870e-7, 2.04426310338993978564e-15,
    ];

    public static double Cdf(double z) => Math.Exp(LogCdf(z));

    public static double LogCdf(double z) =>
        z < -1
            ? Math.Log(0.5 * ComplementaryError(-z / Math.Sqrt(2), scaled: true)) - 0.5 * z * z
            : LogOnePlus.Of(-0.5 * ComplementaryError(z / Math.Sqrt(2), scaled: false));

    public static double LogDensity(double z) => -0.5 * z * z - 0.5 * Math.Log(2 * Math.PI);

    public static double InverseCdf(double p)
    {
        if (p is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(p), p, "A probability is in [0, 1]");

        if (p == 0)
            return double.NegativeInfinity;

        if (p == 1)
            return double.PositiveInfinity;

        var q = p - 0.5;

        if (Math.Abs(q) <= 0.425)
        {
            var r = 0.180625 - q * q;

            return q * Polynomial(CentralNumerator, r) / Polynomial(CentralDenominator, r);
        }

        var tail = Math.Sqrt(-Math.Log(q < 0 ? p : 1 - p));
        var value = tail <= 5
            ? Polynomial(NearNumerator, tail - 1.6) / Polynomial(NearDenominator, tail - 1.6)
            : Polynomial(FarNumerator, tail - 5) / Polynomial(FarDenominator, tail - 5);

        return q < 0 ? -value : value;
    }

    private static double Polynomial(double[] coefficients, double x)
    {
        var value = 0.0;

        for (var i = coefficients.Length - 1; i >= 0; i--)
            value = value * x + coefficients[i];

        return value;
    }

    private static double ComplementaryError(double x, bool scaled)
    {
        var y = Math.Abs(x);
        double result;

        if (y <= Threshold)
        {
            var square = y > Small ? y * y : 0;
            var numerator = A[4] * square;
            var denominator = square;

            for (var i = 0; i < 3; i++)
            {
                numerator = (numerator + A[i]) * square;
                denominator = (denominator + B[i]) * square;
            }

            result = 1 - x * (numerator + A[3]) / (denominator + B[3]);

            return scaled ? Math.Exp(square) * result : result;
        }

        if (y <= 4)
        {
            var numerator = C[8] * y;
            var denominator = y;

            for (var i = 0; i < 7; i++)
            {
                numerator = (numerator + C[i]) * y;
                denominator = (denominator + D[i]) * y;
            }

            result = (numerator + C[7]) / (denominator + D[7]);

            if (!scaled)
                result *= Gaussian(y);
        }
        else if (y >= Big && (!scaled || y >= Largest))
        {
            result = 0;
        }
        else if (y >= Big && y >= Huge)
        {
            result = ReciprocalSquareRootOfPi / y;
        }
        else
        {
            var square = 1 / (y * y);
            var numerator = P[5] * square;
            var denominator = square;

            for (var i = 0; i < 4; i++)
            {
                numerator = (numerator + P[i]) * square;
                denominator = (denominator + Q[i]) * square;
            }

            result = square * (numerator + P[4]) / (denominator + Q[4]);
            result = (ReciprocalSquareRootOfPi - result) / y;

            if (!scaled)
                result *= Gaussian(y);
        }

        if (x >= 0)
            return result;

        if (!scaled)
            return 2 - result;

        if (x < LowestScaled)
            return double.PositiveInfinity;

        return 2 / Gaussian(x) - result;
    }

    private static double Gaussian(double y)
    {
        var truncated = Math.Truncate(y * 16) / 16;
        var remainder = (y - truncated) * (y + truncated);

        return Math.Exp(-truncated * truncated) * Math.Exp(-remainder);
    }
}
