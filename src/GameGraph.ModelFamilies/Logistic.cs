namespace GameGraph.ModelFamilies;

public static class Logistic
{
    public static double Probability(double[] parameters, double[] features) =>
        1 / (1 + Math.Exp(-Linear(parameters, features)));

    public static double NegativeLogLikelihood(double[] parameters, double[] features, int outcome)
    {
        var linear = Linear(parameters, features);

        return outcome switch
        {
            1 => Softplus(-linear),
            0 => Softplus(linear),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The outcome must be 0 or 1"),
        };
    }

    public static double[] Gradient(double[] parameters, double[] features, int outcome)
    {
        if (outcome is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The outcome must be 0 or 1");

        var error = Probability(parameters, features) - outcome;
        var gradient = new double[parameters.Length];

        gradient[0] = error;

        for (var i = 0; i < features.Length; i++)
            gradient[i + 1] = error * features[i];

        return gradient;
    }

    private static double Linear(double[] parameters, double[] features)
    {
        if (parameters.Length != features.Length + 1)
            throw new ArgumentException($"{features.Length} features need {features.Length + 1} parameters (intercept first), got {parameters.Length}", nameof(parameters));

        var linear = parameters[0];

        for (var i = 0; i < features.Length; i++)
            linear += parameters[i + 1] * features[i];

        return linear;
    }

    private static double Softplus(double x) => Math.Max(x, 0) + double.LogP1(Math.Exp(-Math.Abs(x)));
}
