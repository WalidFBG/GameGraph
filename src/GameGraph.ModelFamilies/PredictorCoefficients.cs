namespace GameGraph.ModelFamilies;

public sealed record PredictorCoefficients(double Intercept, TermCoefficients[] Terms);
