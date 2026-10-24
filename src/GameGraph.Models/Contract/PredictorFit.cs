namespace GameGraph.Models.Contract;

public sealed record PredictorFit(double Intercept, IReadOnlyDictionary<string, TermFit> Terms);
