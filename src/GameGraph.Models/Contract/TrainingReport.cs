namespace GameGraph.Models.Contract;

public sealed record TrainingReport
{
    public TrainingReport(
        string node,
        DateTime createdUtc,
        int trainingGames,
        TrainingAndValidation<int> examples,
        int chosen,
        Attempt[] attempts,
        Failure[] failures)
    {
        Node = node;
        CreatedUtc = Utc.Require(createdUtc);
        TrainingGames = trainingGames;
        Examples = examples;
        if (chosen < 0 || chosen >= attempts.Length)
            throw new ArgumentOutOfRangeException(nameof(chosen), chosen, $"{node} chose attempt {chosen} of {attempts.Length}");

        Chosen = chosen;
        Attempts = attempts;
        Failures = failures;
    }

    public string Node { get; }

    public DateTime CreatedUtc { get; }

    public int TrainingGames { get; }

    public TrainingAndValidation<int> Examples { get; }

    public int Chosen { get; }

    public Attempt[] Attempts { get; }

    public Failure[] Failures { get; }

    public Attempt ChosenAttempt() => Attempts[Chosen];
}
