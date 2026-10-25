using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Augmentation;

public sealed class TeamHistory
{
    public const int GamesKept = 20;

    private readonly Dictionary<long, Queue<TeamGame>> _games = [];

    public bool IsComplete(ObservedGame game) => Count(game.HomeTeamId) == GamesKept && Count(game.AwayTeamId) == GamesKept;

    public double PointsPerPossession(ObservedGame game, int side)
    {
        if (!IsComplete(game))
            throw new InvalidOperationException($"Game {game.Id}: a team has fewer than {GamesKept} games of history");

        var history = _games[side == Sides.Home ? game.HomeTeamId : game.AwayTeamId];
        return (double)history.Sum(g => g.Points) / history.Sum(g => g.Possessions);
    }

    public void Advance(ObservedGame game)
    {
        if (!game.IsAccepted)
            throw new InvalidOperationException($"Game {game.Id} was rejected ({game.Rejection}) and cannot advance the history");

        Add(game.HomeTeamId, Summarise(game, Sides.Home));
        Add(game.AwayTeamId, Summarise(game, Sides.Away));
    }

    private int Count(long teamId) => _games.TryGetValue(teamId, out var history) ? history.Count : 0;

    private void Add(long teamId, TeamGame game)
    {
        if (!_games.TryGetValue(teamId, out var history))
            _games[teamId] = history = new Queue<TeamGame>();

        history.Enqueue(game);

        if (history.Count > GamesKept)
            history.Dequeue();
    }

    private static TeamGame Summarise(ObservedGame game, int side)
    {
        var possessions = game.Possessions.Where(p => p.TeamInPossession == side).ToArray();
        return new TeamGame(possessions.Length, game.Points(side));
    }
}
