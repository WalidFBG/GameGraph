using GameGraph.ModelFamilies;

namespace GameGraph.Training;

public sealed record NodeTrainingResult(Fit Fit, PredictorCoefficients[] Predictors, string Path);
