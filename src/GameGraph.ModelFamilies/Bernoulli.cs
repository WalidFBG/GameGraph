namespace GameGraph.ModelFamilies;

public readonly struct Bernoulli : IModelFamily
{
    public string Name => nameof(Bernoulli);

    public int Predictors => 1;

    public FamilyDescription Describe() => new(Name, null);

    public double NegativeLogLikelihood(double[] eta, int outcome) => outcome switch
    {
        1 => Softplus(-eta[0]),
        0 => Softplus(eta[0]),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The outcome must be 0 or 1"),
    };

    public double[] Gradient(double[] eta, int outcome)
    {
        if (outcome is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The outcome must be 0 or 1");

        return [Probability(eta[0]) - outcome];
    }

    public int Sample(double[] eta, double uniform)
    {
        if (uniform is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(uniform), uniform, "The uniform must be in [0, 1)");

        return uniform < Probability(eta[0]) ? 1 : 0;
    }

    private static double Probability(double eta) => 1 / (1 + Math.Exp(-eta));

    private static double Softplus(double x) => Math.Max(x, 0) + double.LogP1(Math.Exp(-Math.Abs(x)));
}
