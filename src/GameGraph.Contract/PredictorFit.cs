using GameGraph.Contract.Terms;

namespace GameGraph.Contract;

public sealed record PredictorFit(double Intercept, IReadOnlyDictionary<string, TermFit> Terms);
