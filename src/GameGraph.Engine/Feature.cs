namespace GameGraph.Engine;

public sealed record Feature<TState>(string Name, Func<TState, double> Compute);
