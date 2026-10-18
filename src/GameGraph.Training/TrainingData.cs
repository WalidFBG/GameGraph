namespace GameGraph.Training;

public sealed class TrainingData
{
    public TrainingData(string node, string[] features, double[][] examples, int[] outcomes)
    {
        if (examples.Length != outcomes.Length)
            throw new ArgumentException($"{examples.Length} examples but {outcomes.Length} outcomes", nameof(outcomes));

        if (examples.Length == 0)
            throw new ArgumentException($"No examples for {node}", nameof(examples));

        Node = node;
        Features = features;
        Examples = examples;
        Outcomes = outcomes;
    }

    public string Node { get; }

    public string[] Features { get; }

    public double[][] Examples { get; }

    public int[] Outcomes { get; }
}
