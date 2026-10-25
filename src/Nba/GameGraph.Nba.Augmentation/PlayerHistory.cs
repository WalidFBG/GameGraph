using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Augmentation;

public sealed class PlayerHistory
{
    public const int GamesKept = 20;

    public const double PriorAttempts = 5;

    private const double PriorShare = 1.0 / Lineup.Size;

    private readonly Dictionary<long, Queue<PlayerGame>> _games = [];

    public double[] LogShares(AcceptedGame game, int points) => [.. game.Players.Select(id => Math.Log(Share(id, points)))];

    public void Advance(AcceptedGame game)
    {
        var games = new PlayerGame[game.Players.Length];

        foreach (var possession in game.Possessions)
        {
            foreach (var shot in possession.Actions.OfType<ObservedShot>())
            {
                foreach (var player in game.Rotation.FiveAt(possession.TeamInPossession, possession.Period, shot.Clock))
                    games[player] = games[player].WithTeamAttempt(shot.Points);

                games[shot.Shooter] = games[shot.Shooter].WithAttempt(shot.Points);
            }
        }

        for (var player = 0; player < games.Length; player++)
        {
            if (games[player].TeamTwos + games[player].TeamThrees > 0)
                Add(game.Players[player], games[player]);
        }
    }

    private double Share(long player, int points)
    {
        if (!_games.TryGetValue(player, out var history))
            return PriorShare;

        var attempts = history.Sum(g => points == 3 ? g.Threes : g.Twos);
        var teamAttempts = history.Sum(g => points == 3 ? g.TeamThrees : g.TeamTwos);

        return (attempts + PriorAttempts * PriorShare) / (teamAttempts + PriorAttempts);
    }

    private void Add(long player, PlayerGame game)
    {
        if (!_games.TryGetValue(player, out var history))
            _games[player] = history = new Queue<PlayerGame>();

        history.Enqueue(game);

        if (history.Count > GamesKept)
            history.Dequeue();
    }

    private readonly record struct PlayerGame(int Twos, int TeamTwos, int Threes, int TeamThrees)
    {
        public PlayerGame WithAttempt(int points) => points == 3 ? this with { Threes = Threes + 1 } : this with { Twos = Twos + 1 };

        public PlayerGame WithTeamAttempt(int points) => points == 3 ? this with { TeamThrees = TeamThrees + 1 } : this with { TeamTwos = TeamTwos + 1 };
    }
}
