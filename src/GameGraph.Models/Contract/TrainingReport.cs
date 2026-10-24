namespace GameGraph.Models.Contract;

public sealed record TrainingReport
{
    public TrainingReport(
        string node,
        DateTime createdUtc,
        int trainingGames,
        TrainingAndValidation<int> examples,
        TrainingAndValidation<double> negativeLogLikelihood,
        Route route,
        double ridge,
        Attempt[] attempts,
        Failure[] failures)
    {
        Node = node;
        CreatedUtc = Utc.Require(createdUtc);
        TrainingGames = trainingGames;
        Examples = examples;
        NegativeLogLikelihood = negativeLogLikelihood;
        Route = route;
        Ridge = ridge;
        Attempts = attempts;
        Failures = failures;
    }

    public string Node { get; }

    public DateTime CreatedUtc { get; }

    public int TrainingGames { get; }

    public TrainingAndValidation<int> Examples { get; }

    public TrainingAndValidation<double> NegativeLogLikelihood { get; }

    public Route Route { get; }

    public double Ridge { get; }

    public Attempt[] Attempts { get; }

    public Failure[] Failures { get; }
}
