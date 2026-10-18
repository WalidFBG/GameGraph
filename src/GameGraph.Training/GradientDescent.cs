using GameGraph.ModelFamilies;

namespace GameGraph.Training;

public static class GradientDescent
{
    public const int Steps = 200;
    public const double LearningRate = 1.0;
    public const int Patience = 5;
    public const double MinimumImprovement = 1e-7;

    public static Fit Fit(Model model, TrainingData training, TrainingData validation)
    {
        var trainingDesigns = training.Examples.Select(model.Design).ToArray();
        var scaling = new DesignScaling(trainingDesigns, training.Node);
        var trainingExamples = trainingDesigns.Select(scaling.Scale).ToArray();
        var validationExamples = validation.Examples.Select(model.Design).Select(scaling.Scale).ToArray();
        var parameters = new double[model.ParameterCount];
        var trainingNegativeLogLikelihoods = new List<double>();
        var validationNegativeLogLikelihoods = new List<double>();
        var bestParameters = parameters.ToArray();
        var bestStep = 0;

        for (var step = 0; step <= Steps; step++)
        {
            trainingNegativeLogLikelihoods.Add(MeanNegativeLogLikelihood(model, parameters, trainingExamples, training.Outcomes));
            validationNegativeLogLikelihoods.Add(MeanNegativeLogLikelihood(model, parameters, validationExamples, validation.Outcomes));

            if (validationNegativeLogLikelihoods[step] < validationNegativeLogLikelihoods[bestStep] - MinimumImprovement)
            {
                bestParameters = parameters.ToArray();
                bestStep = step;
            }

            if (step - bestStep == Patience || step == Steps)
                break;

            var gradient = MeanGradient(model, parameters, trainingExamples, training.Outcomes);

            for (var i = 0; i < parameters.Length; i++)
                parameters[i] -= LearningRate * gradient[i];
        }

        return new Fit(scaling.Unscale(bestParameters, model.Family.Predictors), bestStep, trainingNegativeLogLikelihoods.ToArray(), validationNegativeLogLikelihoods.ToArray());
    }

    private static double MeanNegativeLogLikelihood(Model model, double[] parameters, double[][] designs, int[] outcomes)
    {
        var total = 0.0;

        for (var e = 0; e < designs.Length; e++)
            total += model.NegativeLogLikelihood(parameters, designs[e], outcomes[e]);

        return total / designs.Length;
    }

    private static double[] MeanGradient(Model model, double[] parameters, double[][] designs, int[] outcomes)
    {
        var total = new double[parameters.Length];

        for (var e = 0; e < designs.Length; e++)
        {
            var gradient = model.Gradient(parameters, designs[e], outcomes[e]);

            for (var i = 0; i < total.Length; i++)
                total[i] += gradient[i];
        }

        for (var i = 0; i < total.Length; i++)
            total[i] /= designs.Length;

        return total;
    }
}
