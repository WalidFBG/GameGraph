namespace GameGraph.Contract.Terms;

public sealed record LinearFit(double Centre, double Scale, double[] Coefficients) : TermFit(Coefficients);
