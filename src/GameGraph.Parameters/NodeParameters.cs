using GameGraph.ModelFamilies;

namespace GameGraph.Parameters;

public sealed record NodeParameters(string Node, string Family, PredictorCoefficients[] Predictors);
