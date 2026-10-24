namespace GameGraph.Models.Contract;

public sealed record Attempt(Route Route, double Ridge, TrainingAndValidation<double> NegativeLogLikelihood, int Iterations);
