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

    public const double PriorAttempts = 20;

    public const double PriorChances = 50;

    public const double PriorActions = 100;

    public const int LeagueGamesKept = 300;

    private const double PriorShare = 1.0 / Lineups.Size;

    private static readonly PlayerEvent[] Events = Enum.GetValues<PlayerEvent>();

    private readonly Dictionary<long, Queue<PlayerGame>> _games = [];
    private readonly Queue<PlayerGame> _league = new();

    public double[][] LogShares(AcceptedGame game) =>
        [.. Events.Select(e => e == PlayerEvent.NotSet ? [] : game.Players.Select(id => Math.Log(Share(id, e))).ToArray())];

    public PlayerRates Rates(AcceptedGame game) =>
        new(
            Assisted: Rates(game, g => g.AssistedMakes, g => g.Makes, PriorShots),
            Blocked: Rates(game, g => g.BlockedShots, g => g.Shots, PriorShots),
            TwoMakes: Rates(game, g => g.TwoMakes, g => g.Events[(int)PlayerEvent.TwoPointAttempt], PriorAttempts),
            ThreeMakes: Rates(game, g => g.ThreeMakes, g => g.Events[(int)PlayerEvent.ThreePointAttempt], PriorAttempts),
            LogTwoFouledRatio: LogRatios(game, g => g.FouledTwos, g => g.Events[(int)PlayerEvent.TwoPointAttempt], PriorAttempts),
            LogThreeFouledRatio: LogRatios(game, g => g.FouledThrees, g => g.Events[(int)PlayerEvent.ThreePointAttempt], PriorAttempts),
            FreeThrows: Rates(game, g => g.FreeThrowMakes, g => g.FreeThrows, PriorAttempts),
            TwoAttempts: Rates(game, g => g.Events[(int)PlayerEvent.TwoPointAttempt], g => g.OffensiveActions, PriorActions),
            ThreeAttempts: Rates(game, g => g.Events[(int)PlayerEvent.ThreePointAttempt], g => g.OffensiveActions, PriorActions),
            FoulsDrawn: Rates(game, g => g.Events[(int)PlayerEvent.FoulDrawn], g => g.OffensiveActions, PriorActions),
            OffensiveRebounds: Rates(game, g => g.Events[(int)PlayerEvent.OffensiveRebound], g => g.OffensiveChances, PriorChances),
            DefensiveRebounds: Rates(game, g => g.Events[(int)PlayerEvent.DefensiveRebound], g => g.DefensiveChances, PriorChances),
            Steals: Rates(game, g => g.Events[(int)PlayerEvent.Steal], g => g.DefensiveActions, PriorActions),
            Blocks: Rates(game, g => g.Events[(int)PlayerEvent.Block], g => g.OpponentShots, PriorActions),
            ShootingFouls: Rates(game, g => g.ShootingFouls, g => g.OpponentShots, PriorActions));

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
                CreditAwarded(games, court, action.AwardedBefore);

                foreach (var player in court[offence])
                    games[player].OffensiveActions++;

                foreach (var player in court[defence])
                    games[player].DefensiveActions++;

                switch (action)
                {
                    case ObservedShot shot:
                        Credit(games, court[offence], shot.Shooter, shot.Points == 3 ? PlayerEvent.ThreePointAttempt : PlayerEvent.TwoPointAttempt);
                        games[shot.Shooter].Shots++;

                        if (shot.Fouler is { } fouler)
                        {
                            Credit(games, court[defence], fouler, PlayerEvent.Foul);
                            games[fouler].ShootingFouls++;
                        }

                        foreach (var player in court[defence])
                            games[player].OpponentShots++;

                        if (shot.Made)
                        {
                            games[shot.Shooter].Makes++;

                            if (shot.Points == 3)
                                games[shot.Shooter].ThreeMakes++;
                            else
                                games[shot.Shooter].TwoMakes++;
                        }

                        if (shot.Fouled)
                        {
                            if (shot.Points == 3)
                                games[shot.Shooter].FouledThrees++;
                            else
                                games[shot.Shooter].FouledTwos++;
                        }

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
                {
                    Substitute(court, freeThrow.Before);

                    var shooter = FreeThrowShooterOf(action);

                    games[shooter].FreeThrows++;

                    if (freeThrow.Made)
                        games[shooter].FreeThrowMakes++;
                }

                if (ReboundOf(action) is not null)
                {
                    foreach (var player in court[offence])
                        games[player].OffensiveChances++;

                    foreach (var player in court[defence])
                        games[player].DefensiveChances++;
                }

                if (ReboundOf(action) is { Rebounder: { } rebounder } rebound)
                    Credit(games, court[rebound.Offensive ? offence : defence], rebounder, rebound.Offensive ? PlayerEvent.OffensiveRebound : PlayerEvent.DefensiveRebound);
            }

            Substitute(court, possession.After);
            CreditAwarded(games, court, possession.AwardedAfter);
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

        _league.Enqueue(PlayerGame.Sum(games));

        if (_league.Count > LeagueGamesKept)
            _league.Dequeue();
    }

    private static void CreditAwarded(PlayerGame[] games, int[][] court, AwardedFreeThrow[] awarded)
    {
        foreach (var freeThrow in awarded)
        {
            Credit(games, court[freeThrow.Team], freeThrow.Shooter, PlayerEvent.AwardedFreeThrow);
            games[freeThrow.Shooter].FreeThrows++;

            if (freeThrow.Made)
                games[freeThrow.Shooter].FreeThrowMakes++;

            if (freeThrow.Fouler is { } fouler)
                Credit(games, court[Sides.Opponent(freeThrow.Team)], fouler, PlayerEvent.Foul);
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

    private static int FreeThrowShooterOf(ObservedAction action) => action switch
    {
        ObservedShot shot => shot.Shooter,
        ObservedFoul foul => foul.FouledPlayer,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name, "Only a shot or a foul gives free throws"),
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

    private double[] Rates(AcceptedGame game, Func<PlayerGame, int> events, Func<PlayerGame, int> chances, double weight)
    {
        var league = LeagueRate(events, chances);

        return [.. game.Players.Select(id => Rate(id, events, chances, league, weight))];
    }

    private double[] LogRatios(AcceptedGame game, Func<PlayerGame, int> events, Func<PlayerGame, int> chances, double weight)
    {
        var league = LeagueRate(events, chances);

        return [.. game.Players.Select(id => Math.Log(Rate(id, events, chances, league, weight) / league))];
    }

    private double LeagueRate(Func<PlayerGame, int> events, Func<PlayerGame, int> chances)
    {
        var tries = _league.Sum(chances);

        return tries > 0
            ? (double)_league.Sum(events) / tries
            : throw new InvalidOperationException("A league rate needs games before it");
    }

    private double Rate(long player, Func<PlayerGame, int> events, Func<PlayerGame, int> chances, double prior, double weight)
    {
        var (hits, tries) = _games.TryGetValue(player, out var history) ? (history.Sum(events), history.Sum(chances)) : (0, 0);

        return (hits + weight * prior) / (tries + weight);
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

        public int TwoMakes { get; set; }

        public int ThreeMakes { get; set; }

        public int FouledTwos { get; set; }

        public int FouledThrees { get; set; }

        public int FreeThrows { get; set; }

        public int FreeThrowMakes { get; set; }

        public int OffensiveChances { get; set; }

        public int OffensiveActions { get; set; }

        public int ShootingFouls { get; set; }

        public int OpponentShots { get; set; }

        public int DefensiveActions { get; set; }

        public int DefensiveChances { get; set; }

        public int Seconds { get; set; }

        public bool Started { get; set; }

        public static PlayerGame Sum(PlayerGame[] games)
        {
            var sum = new PlayerGame();

            foreach (var game in games)
            {
                for (var e = 0; e < sum.Events.Length; e++)
                {
                    sum.Events[e] += game.Events[e];
                    sum.TeamEvents[e] += game.TeamEvents[e];
                }

                sum.Shots += game.Shots;
                sum.Makes += game.Makes;
                sum.AssistedMakes += game.AssistedMakes;
                sum.BlockedShots += game.BlockedShots;
                sum.TwoMakes += game.TwoMakes;
                sum.ThreeMakes += game.ThreeMakes;
                sum.FouledTwos += game.FouledTwos;
                sum.FouledThrees += game.FouledThrees;
                sum.FreeThrows += game.FreeThrows;
                sum.FreeThrowMakes += game.FreeThrowMakes;
                sum.OffensiveChances += game.OffensiveChances;
                sum.OffensiveActions += game.OffensiveActions;
                sum.ShootingFouls += game.ShootingFouls;
                sum.OpponentShots += game.OpponentShots;
                sum.DefensiveActions += game.DefensiveActions;
                sum.DefensiveChances += game.DefensiveChances;
            }

            return sum;
        }
    }
}
