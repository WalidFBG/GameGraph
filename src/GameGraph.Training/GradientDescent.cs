using GameGraph.ModelFamilies;

namespace GameGraph.Training;

public static class GradientDescent
{
    public const int Steps = 200;
    public const double LearningRate = 1.0;

    public static Fit Fit(TrainingData data)
    {
        var scaling = new FeatureScaling(data);
        var examples = data.Examples.Select(scaling.Scale).ToArray();
        var parameters = new double[data.Features.Length + 1];
        var negativeLogLikelihoods = new double[Steps + 1];

        for (var step = 0; step < Steps; step++)
        {
            negativeLogLikelihoods[step] = MeanNegativeLogLikelihood(parameters, examples, data.Outcomes);

            var gradient = MeanGradient(parameters, examples, data.Outcomes);

            for (var i = 0; i < parameters.Length; i++)
                parameters[i] -= LearningRate * gradient[i];
        }

        negativeLogLikelihoods[Steps] = MeanNegativeLogLikelihood(parameters, examples, data.Outcomes);

        return new Fit(scaling.Unscale(parameters), negativeLogLikelihoods);
    }

    private static double MeanNegativeLogLikelihood(double[] parameters, double[][] examples, int[] outcomes)
    {
        var total = 0.0;

        for (var e = 0; e < examples.Length; e++)
            total += Logistic.NegativeLogLikelihood(parameters, examples[e], outcomes[e]);

        return total / examples.Length;
    }

    private static double[] MeanGradient(double[] parameters, double[][] examples, int[] outcomes)
    {
        var total = new double[parameters.Length];

        for (var e = 0; e < examples.Length; e++)
        {
            var gradient = Logistic.Gradient(parameters, examples[e], outcomes[e]);

            for (var i = 0; i < total.Length; i++)
                total[i] += gradient[i];
        }

        for (var i = 0; i < total.Length; i++)
            total[i] /= examples.Length;

        return total;
    }
}
