using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Augmentation;

public sealed class PlayerHistory
{
    public const int GamesKept = 20;

    public const double PriorEvents = 5;

    public const double PriorGames = 2;

    public const double PriorSeconds = 600;

    public const double PriorShots = 10;

    public const double PriorAssisted = 0.63;

    public const double PriorBlocked = 0.055;

    private const double PriorShare = 1.0 / Lineups.Size;

    private static readonly PlayerEvent[] Events = Enum.GetValues<PlayerEvent>();

    private readonly Dictionary<long, Queue<PlayerGame>> _games = [];

    public double[][] LogShares(AcceptedGame game) =>
        [.. Events.Select(e => e == PlayerEvent.NotSet ? [] : game.Players.Select(id => Math.Log(Share(id, e))).ToArray())];

    public double[] AssistedRates(AcceptedGame game) => [.. game.Players.Select(id => Rate(id, g => g.AssistedMakes, g => g.Makes, PriorAssisted))];

    public double[] BlockedRates(AcceptedGame game) => [.. game.Players.Select(id => Rate(id, g => g.BlockedShots, g => g.Shots, PriorBlocked))];

    public double[] UsualSeconds(AcceptedGame game) => [.. game.Players.Select(id => Shrunk(id, g => g.Seconds, PriorSeconds))];

    public double[] StartShares(AcceptedGame game) => [.. game.Players.Select(id => Shrunk(id, g => g.Started ? 1 : 0, 0))];

    public double[] StartedLastGame(AcceptedGame game) => [.. game.Players.Select(id => _games.TryGetValue(id, out var history) && history.Last().Started ? 1.0 : 0)];

    public void Advance(AcceptedGame game)
    {
        var games = game.Players.Select(_ => new PlayerGame()).ToArray();
        int[][] court = [.. game.Roster.Starters.Select(s => s.ToArray())];

        foreach (var possession in game.Possessions)
        {
            var offence = possession.TeamInPossession;
            var defence = Sides.Opponent(offence);

            foreach (var action in possession.Actions)
            {
                Substitute(court, action.Before);

                switch (action)
                {
                    case ObservedShot shot:
                        Credit(games, court[offence], shot.Shooter, shot.Points == 3 ? PlayerEvent.ThreePointAttempt : PlayerEvent.TwoPointAttempt);
                        games[shot.Shooter].Shots++;

                        if (shot.Fouler is { } fouler)
                            Credit(games, court[defence], fouler, PlayerEvent.Foul);

                        if (shot.Made)
                            games[shot.Shooter].Makes++;

                        if (shot.Assister is { } assister)
                        {
                            Credit(games, court[offence], assister, PlayerEvent.Assist);
                            games[shot.Shooter].AssistedMakes++;
                        }

                        if (shot.Blocker is { } blocker)
                        {
                            Credit(games, court[defence], blocker, PlayerEvent.Block);
                            games[shot.Shooter].BlockedShots++;
                        }

                        break;
                    case ObservedFoul foul:
                        Credit(games, court[defence], foul.Fouler, PlayerEvent.Foul);
                        Credit(games, court[offence], foul.FouledPlayer, PlayerEvent.FoulDrawn);
                        break;
                    case ObservedTurnover { Player: { } player } turnover:
                        Credit(games, court[offence], player, turnover.Kind == TurnoverKinds.OffensiveFoul ? PlayerEvent.OffensiveFoul : PlayerEvent.Turnover);

                        if (turnover.Stealer is { } stealer)
                            Credit(games, court[defence], stealer, PlayerEvent.Steal);

                        break;
                }

                foreach (var freeThrow in FreeThrowsOf(action))
                    Substitute(court, freeThrow.Before);

                if (ReboundOf(action) is { Rebounder: { } rebounder } rebound)
                    Credit(games, court[rebound.Offensive ? offence : defence], rebounder, rebound.Offensive ? PlayerEvent.OffensiveRebound : PlayerEvent.DefensiveRebound);
            }

            Substitute(court, possession.After);
        }

        for (var team = 0; team < game.Roster.Active.Length; team++)
        {
            foreach (var player in game.Roster.Active[team])
            {
                games[player].Seconds = game.Roster.SecondsPlayed[player];
                games[player].Started = game.Roster.Starters[team].Contains(player);
                Add(game.Players[player], games[player]);
            }
        }
    }

    private static void Credit(PlayerGame[] games, int[] court, int player, PlayerEvent e)
    {
        foreach (var teammate in court)
            games[teammate].TeamEvents[(int)e]++;

        games[player].Events[(int)e]++;
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

    private static ObservedRebound? ReboundOf(ObservedAction action) => action switch
    {
        ObservedShot shot => shot.Rebound,
        ObservedFoul foul => foul.Rebound,
        ObservedTurnover => null,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name, "Not an observed action"),
    };

    private double Shrunk(long player, Func<PlayerGame, double> value, double prior)
    {
        var (sum, games) = _games.TryGetValue(player, out var history) ? (history.Sum(value), history.Count) : (0.0, 0);

        return (sum + PriorGames * prior) / (games + PriorGames);
    }

    private double Share(long player, PlayerEvent e)
    {
        if (!_games.TryGetValue(player, out var history))
            return PriorShare;

        var events = history.Sum(g => g.Events[(int)e]);
        var teamEvents = history.Sum(g => g.TeamEvents[(int)e]);

        return (events + PriorEvents * PriorShare) / (teamEvents + PriorEvents);
    }

    private double Rate(long player, Func<PlayerGame, int> events, Func<PlayerGame, int> chances, double prior)
    {
        var (hits, tries) = _games.TryGetValue(player, out var history) ? (history.Sum(events), history.Sum(chances)) : (0, 0);

        return (hits + PriorShots * prior) / (tries + PriorShots);
    }

    private void Add(long player, PlayerGame game)
    {
        if (!_games.TryGetValue(player, out var history))
            _games[player] = history = new Queue<PlayerGame>();

        history.Enqueue(game);

        if (history.Count > GamesKept)
            history.Dequeue();
    }

    private sealed class PlayerGame
    {
        public int[] Events { get; } = new int[PlayerHistory.Events.Length];

        public int[] TeamEvents { get; } = new int[PlayerHistory.Events.Length];

        public int Shots { get; set; }

        public int Makes { get; set; }

        public int AssistedMakes { get; set; }

        public int BlockedShots { get; set; }

        public int Seconds { get; set; }

        public bool Started { get; set; }
    }
}
