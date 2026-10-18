using GameGraph.ModelFamilies;

namespace GameGraph.Training;

public sealed class NodeTrainer(ParametersWriter writer)
{
    public NodeTrainingResult Train(TrainingData training, TrainingData validation, string source)
    {
        var model = training.Model.Create(training.Features);
        var fit = GradientDescent.Fit(model, training, validation);

        return new NodeTrainingResult(fit, model.Describe(fit.Parameters), writer.Write(training, model, fit, source));
    }
}
