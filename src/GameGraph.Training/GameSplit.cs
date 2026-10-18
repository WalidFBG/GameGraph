namespace GameGraph.Training;

public static class GameSplit
{
    public const double ValidationFraction = 0.2;

    public static (TrainingData Training, TrainingData Validation) ByStartTime(TrainingData data)
    {
        var games = data.Games.OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal).ToArray();
        var validation = (int)Math.Round(games.Length * ValidationFraction);

        if (validation == 0 || validation == games.Length)
            throw new InvalidDataException($"{games.Length} games of {data.Node} cannot be split {1 - ValidationFraction:P0} / {ValidationFraction:P0}");

        return (Subset(data, games[..^validation]), Subset(data, games[^validation..]));
    }

    private static TrainingData Subset(TrainingData data, TrainingGame[] games)
    {
        var subset = new List<TrainingGame>(games.Length);
        var examples = new List<double[]>();
        var outcomes = new List<int>();

        foreach (var game in games)
        {
            subset.Add(game with { FirstExample = examples.Count });

            for (var e = game.FirstExample; e < game.FirstExample + game.Examples; e++)
            {
                examples.Add(data.Examples[e]);
                outcomes.Add(data.Outcomes[e]);
            }
        }

        return new TrainingData(data.Node, data.Model, data.Features, subset.ToArray(), examples.ToArray(), outcomes.ToArray());
    }
}
