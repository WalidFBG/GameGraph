using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
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

        Possession[] possessions;
        int cut;

        try
        {
            var parser = new PossessionParser(TeamsByPlayer(game));

            foreach (var e in game.Events)
                parser.Read(e);

            possessions = parser.Build();
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

        return Observed(game, RejectionReason.NotSet, possessions, cut);
    }

    private static bool BreaksAlternation(Possession[] possessions)
    {
        var starters = possessions.GroupBy(p => p.Period).ToDictionary(g => g.Key, g => g.First().TeamInPossession);
        var first = starters[0];

        return starters[1] != Sides.Opponent(first) || starters[2] != Sides.Opponent(first) || starters[3] != first;
    }

    private static ObservedGame Rejected(Game game, RejectionReason reason) => Observed(game, reason, [], 0);

    private static ObservedGame Observed(Game game, RejectionReason rejection, Possession[] possessions, int cut) =>
        new(game.SourceId, game.ScheduledStartTimeUtc, game.TeamA.Team.Id, game.TeamB.Team.Id, rejection, possessions, cut);

    private static Dictionary<long, TeamType> TeamsByPlayer(Game game) =>
        game.TeamA.Players.Select(p => (p.Player.Id, TeamType.HomeOrA))
            .Concat(game.TeamB.Players.Select(p => (p.Player.Id, TeamType.AwayOrB)))
            .ToDictionary();
}
