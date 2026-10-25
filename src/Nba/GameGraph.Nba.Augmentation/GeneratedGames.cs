using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Augmentation;

public static class GeneratedGames
{
    public static IEnumerable<(ObservedGame Game, GameContext Context)> InOrder(IEnumerable<ObservedGame> games)
    {
        var history = new TeamHistory();

        foreach (var game in games.Where(g => g.IsAccepted).OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (history.IsComplete(game))
                yield return (game, history.ContextFor(game));

            history.Advance(game);
        }
    }
}
