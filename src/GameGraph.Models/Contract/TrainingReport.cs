namespace GameGraph.Models.Contract;

public sealed record TrainingReport
{
    public TrainingReport(
        string node,
        DateTime createdUtc,
        int trainingGames,
        TrainingAndValidation<int> examples,
        TrainingAndValidation<double> negativeLogLikelihood,
        Optimiser optimiser)
    {
        Node = node;
        CreatedUtc = Utc.Require(createdUtc);
        TrainingGames = trainingGames;
        Examples = examples;
        NegativeLogLikelihood = negativeLogLikelihood;
        Optimiser = optimiser;
    }

    public string Node { get; }

    public DateTime CreatedUtc { get; }

    public int TrainingGames { get; }

    public TrainingAndValidation<int> Examples { get; }

    public TrainingAndValidation<double> NegativeLogLikelihood { get; }

    public Optimiser Optimiser { get; }
}
