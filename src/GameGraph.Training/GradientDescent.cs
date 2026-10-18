using GameGraph.ModelFamilies;

namespace GameGraph.Training;

public static class GradientDescent
{
    public const int Steps = 200;
    public const double LearningRate = 1.0;
    public const int Patience = 5;
    public const double MinimumImprovement = 1e-7;

    public static Fit Fit(TrainingData training, TrainingData validation)
    {
        var scaling = new FeatureScaling(training);
        var trainingExamples = training.Examples.Select(scaling.Scale).ToArray();
        var validationExamples = validation.Examples.Select(scaling.Scale).ToArray();
        var parameters = new double[training.Features.Length + 1];
        var trainingNegativeLogLikelihoods = new List<double>();
        var validationNegativeLogLikelihoods = new List<double>();
        var bestParameters = parameters.ToArray();
        var bestStep = 0;

        for (var step = 0; step <= Steps; step++)
        {
            trainingNegativeLogLikelihoods.Add(MeanNegativeLogLikelihood(parameters, trainingExamples, training.Outcomes));
            validationNegativeLogLikelihoods.Add(MeanNegativeLogLikelihood(parameters, validationExamples, validation.Outcomes));

            if (validationNegativeLogLikelihoods[step] < validationNegativeLogLikelihoods[bestStep] - MinimumImprovement)
            {
                bestParameters = parameters.ToArray();
                bestStep = step;
            }

            if (step - bestStep == Patience || step == Steps)
                break;

            var gradient = MeanGradient(parameters, trainingExamples, training.Outcomes);

            for (var i = 0; i < parameters.Length; i++)
                parameters[i] -= LearningRate * gradient[i];
        }

        return new Fit(scaling.Unscale(bestParameters), bestStep, trainingNegativeLogLikelihoods.ToArray(), validationNegativeLogLikelihoods.ToArray());
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
