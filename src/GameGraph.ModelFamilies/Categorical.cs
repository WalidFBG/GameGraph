namespace GameGraph.ModelFamilies;

public readonly struct Categorical : IModelFamily
{
    public Categorical(int categories)
    {
        if (categories < 2)
            throw new ArgumentOutOfRangeException(nameof(categories), categories, "A categorical model needs at least 2 categories");

        Categories = categories;
    }

    public string Name => nameof(Categorical);

    public int Categories { get; }

    public int Predictors => Categories - 1;

    public FamilyDescription Describe() => new(Name, Categories);

    public double NegativeLogLikelihood(double[] eta, int outcome)
    {
        CheckOutcome(outcome);

        var scores = Scores(eta);
        var max = scores.Max();

        return max + Math.Log(scores.Sum(s => Math.Exp(s - max))) - scores[outcome];
    }

    public double[] Gradient(double[] eta, int outcome)
    {
        CheckOutcome(outcome);

        var probabilities = Probabilities(eta);
        var gradient = new double[Predictors];

        for (var category = 1; category < Categories; category++)
            gradient[category - 1] = probabilities[category] - (category == outcome ? 1 : 0);

        return gradient;
    }

    public int Sample(double[] eta, double uniform)
    {
        if (uniform is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(uniform), uniform, "The uniform must be in [0, 1)");

        var probabilities = Probabilities(eta);
        var cumulative = 0.0;

        for (var category = 0; category < Categories - 1; category++)
        {
            cumulative += probabilities[category];

            if (uniform < cumulative)
                return category;
        }

        return Categories - 1;
    }

    private double[] Scores(double[] eta)
    {
        if (eta.Length != Predictors)
            throw new ArgumentException($"{Categories} categories need {Predictors} linear predictors, got {eta.Length}", nameof(eta));

        return [0, .. eta];
    }

    private double[] Probabilities(double[] eta)
    {
        var scores = Scores(eta);
        var max = scores.Max();
        var exponentials = scores.Select(s => Math.Exp(s - max)).ToArray();
        var total = exponentials.Sum();

        return exponentials.Select(e => e / total).ToArray();
    }

    private void CheckOutcome(int outcome)
    {
        if (outcome < 0 || outcome >= Categories)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, $"The outcome must be a category from 0 to {Categories - 1}");
    }
}
