namespace GameGraph.Parameters;

public sealed record NodeParameters(string Node, double Intercept, Dictionary<string, double> Weights);
