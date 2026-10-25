using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Replay;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Sportradar;

public static class ObservedGameReader
{
    public const int MaximumPossessionDifference = 4;

    public static ObservedGame Read(Game game)
    {
        if (game.SeasonPhase != SeasonPhase.RegularSeason)
            return Rejected(game, RejectionReason.OutsideRegularSeason);

        if (game.ValidationSummary.HasInvalidEvents)
            return Rejected(game, RejectionReason.InvalidEvents);

        long[] players = [.. game.TeamA.Players.Concat(game.TeamB.Players).Select(p => p.Player.Id)];
        var slots = players.Select((id, slot) => (id, slot)).ToDictionary();
        Possession[] possessions;
        Rotation rotation;
        int cut;

        try
        {
            var parser = new PossessionParser(TeamsByPlayer(game), slots);
            var lineups = new RotationParser(TeamsByPlayer(game), slots);

            foreach (var e in game.Events)
            {
                parser.Read(e);
                lineups.Read(e);
            }

            possessions = parser.Build();
            rotation = lineups.Build(possessions.Max(p => p.Period) + 1);
            cut = parser.PossessionsCutByPeriodEnd;
        }
        catch (RejectedGameException rejected)
        {
            return Rejected(game, rejected.Reason);
        }

        var home = possessions.Count(p => p.TeamInPossession == Sides.Home);

        if (Math.Abs(home - (possessions.Length - home)) > MaximumPossessionDifference)
            return Rejected(game, RejectionReason.UnbalancedPossessions);

        if (BreaksAlternation(possessions))
            return Rejected(game, RejectionReason.PeriodStartBreaksAlternation);

        var accepted = new AcceptedGame(game.SourceId, game.ScheduledStartTimeUtc, game.TeamA.Team.Id, game.TeamB.Team.Id, players, rotation, possessions, cut);

        return ObservedOutcomeDecider.Follows(accepted) ? accepted : Rejected(game, RejectionReason.RulesDisagreeWithData);
    }

    private static bool BreaksAlternation(Possession[] possessions)
    {
        var starters = possessions.GroupBy(p => p.Period).ToDictionary(g => g.Key, g => g.First().TeamInPossession);
        var first = starters[0];

        return starters[1] != Sides.Opponent(first) || starters[2] != Sides.Opponent(first) || starters[3] != first;
    }

    private static RejectedGame Rejected(Game game, RejectionReason reason) =>
        new(game.SourceId, game.ScheduledStartTimeUtc, game.TeamA.Team.Id, game.TeamB.Team.Id, reason);

    private static Dictionary<long, TeamType> TeamsByPlayer(Game game) =>
        game.TeamA.Players.Select(p => (p.Player.Id, TeamType.HomeOrA))
            .Concat(game.TeamB.Players.Select(p => (p.Player.Id, TeamType.AwayOrB)))
            .ToDictionary();
}
