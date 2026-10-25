using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Augmentation;

public static class GeneratedGames
{
    public static IEnumerable<(AcceptedGame Game, GameContext Context)> InOrder(IEnumerable<ObservedGame> games)
    {
        var teams = new TeamHistory();
        var players = new PlayerHistory();

        foreach (var game in games.OfType<AcceptedGame>().OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (teams.IsComplete(game))
                yield return (game, ContextFor(game, teams, players));

            teams.Advance(game);
            players.Advance(game);
        }
    }

    private static GameContext ContextFor(AcceptedGame game, TeamHistory teams, PlayerHistory players) =>
        new(teams.PointsPerPossession(game, Sides.Home), teams.PointsPerPossession(game, Sides.Away), players.LogShares(game, 2), players.LogShares(game, 3));
}
