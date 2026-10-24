namespace GameGraph.Models.Contract;

public sealed record TrainingAndValidation<T>(T Training, T Validation);
