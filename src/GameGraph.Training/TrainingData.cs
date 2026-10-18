using GameGraph.ModelFamilies;

namespace GameGraph.Training;

public sealed class TrainingData
{
    public TrainingData(string node, ModelDescription model, string[] features, TrainingGame[] games, double[][] examples, int[] outcomes)
    {
        if (examples.Length != outcomes.Length)
            throw new ArgumentException($"{examples.Length} examples but {outcomes.Length} outcomes", nameof(outcomes));

        if (examples.Length == 0)
            throw new ArgumentException($"No examples for {node}", nameof(examples));

        var next = 0;

        foreach (var game in games)
        {
            if (game.FirstExample != next)
                throw new ArgumentException($"Game {game.Id} starts at example {game.FirstExample}, expected {next}", nameof(games));

            next += game.Examples;
        }

        if (next != examples.Length)
            throw new ArgumentException($"The games hold {next} examples, there are {examples.Length}", nameof(games));

        Node = node;
        Model = model;
        Features = features;
        Games = games;
        Examples = examples;
        Outcomes = outcomes;
    }

    public string Node { get; }

    public ModelDescription Model { get; }

    public string[] Features { get; }

    public TrainingGame[] Games { get; }

    public double[][] Examples { get; }

    public int[] Outcomes { get; }
}
