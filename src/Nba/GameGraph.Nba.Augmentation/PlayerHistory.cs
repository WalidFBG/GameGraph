using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Augmentation;

public sealed class PlayerHistory
{
    public const int GamesKept = 20;

    public const double PriorAttempts = 5;

    public const double PriorGames = 2;

    public const double PriorSeconds = 600;

    private const double PriorShare = 1.0 / Lineups.Size;

    private readonly Dictionary<long, Queue<PlayerGame>> _games = [];

    public double[] LogShares(AcceptedGame game, int points) => [.. game.Players.Select(id => Math.Log(Share(id, points)))];

    public double[] UsualSeconds(AcceptedGame game) => [.. game.Players.Select(id => Shrunk(id, g => g.Seconds, PriorSeconds))];

    public double[] StartShares(AcceptedGame game) => [.. game.Players.Select(id => Shrunk(id, g => g.Started ? 1 : 0, 0))];

    public double[] StartedLastGame(AcceptedGame game) => [.. game.Players.Select(id => _games.TryGetValue(id, out var history) && history.Last().Started ? 1.0 : 0)];

    public void Advance(AcceptedGame game)
    {
        var games = new PlayerGame[game.Players.Length];
        int[][] court = [.. game.Roster.Starters.Select(s => s.ToArray())];

        foreach (var possession in game.Possessions)
        {
            foreach (var action in possession.Actions)
            {
                Substitute(court, action.Before);

                if (action is ObservedShot shot)
                {
                    foreach (var player in court[possession.TeamInPossession])
                        games[player] = games[player].WithTeamAttempt(shot.Points);

                    games[shot.Shooter] = games[shot.Shooter].WithAttempt(shot.Points);
                }

                foreach (var freeThrow in FreeThrowsOf(action))
                    Substitute(court, freeThrow.Before);
            }

            Substitute(court, possession.After);
        }

        for (var team = 0; team < game.Roster.Active.Length; team++)
        {
            foreach (var player in game.Roster.Active[team])
                Add(game.Players[player], games[player] with { Seconds = game.Roster.SecondsPlayed[player], Started = game.Roster.Starters[team].Contains(player) });
        }
    }

    private static void Substitute(int[][] court, ObservedMoment moment)
    {
        foreach (var change in moment.Substitutions)
        {
            for (var i = 0; i < change.Off.Length; i++)
                court[change.Team][Array.IndexOf(court[change.Team], change.Off[i])] = change.On[i];
        }
    }

    private static ObservedFreeThrow[] FreeThrowsOf(ObservedAction action) => action switch
    {
        ObservedShot shot => shot.FreeThrows,
        ObservedFoul foul => foul.FreeThrows,
        ObservedTurnover => [],
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name, "Not an observed action"),
    };

    private double Shrunk(long player, Func<PlayerGame, double> value, double prior)
    {
        var (sum, games) = _games.TryGetValue(player, out var history) ? (history.Sum(value), history.Count) : (0.0, 0);

        return (sum + PriorGames * prior) / (games + PriorGames);
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

    private readonly record struct PlayerGame(int Twos, int TeamTwos, int Threes, int TeamThrees, int Seconds, bool Started)
    {
        public PlayerGame WithAttempt(int points) => points == 3 ? this with { Threes = Threes + 1 } : this with { Twos = Twos + 1 };

        public PlayerGame WithTeamAttempt(int points) => points == 3 ? this with { TeamThrees = TeamThrees + 1 } : this with { TeamTwos = TeamTwos + 1 };
    }
}
