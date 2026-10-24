namespace GameGraph.Models.Contract;

public sealed record LinearFit(double Centre, double Scale, double[] Coefficients) : TermFit(Coefficients);
