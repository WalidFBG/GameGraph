using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Audit.Domain;

internal static class GeneratedGames
{
    public static IEnumerable<(ObservedGame Game, GameContext Context)> From(IEnumerable<ObservedGame> games)
    {
        var history = new TeamHistory();

        foreach (var game in games.OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (!game.IsAccepted)
                continue;

            if (history.IsComplete(game))
                yield return (game, history.ContextFor(game));

            history.Advance(game);
        }
    }

    public static void RequireTrainingDataFrom(string node, IEnumerable<string> trainingDataGameIds, IEnumerable<ObservedGame> games)
    {
        if (!trainingDataGameIds.SequenceEqual(games.Select(g => g.Id)))
            throw new InvalidDataException($"The training data of {node} was not generated from these games");
    }
}
