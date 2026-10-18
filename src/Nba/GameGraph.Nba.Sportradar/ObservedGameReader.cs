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

        var teams = TeamsByPlayer(game);
        var builder = new PossessionBuilder();

        foreach (var e in game.Events)
        {
            if (e.EventType == EventType.EndPeriod)
            {
                builder.EndPeriod();
                continue;
            }

            if (!IsAction(e))
                continue;

            var team = ActingTeam(e, teams);

            if (team == TeamType.NotSet)
                return Rejected(game, RejectionReason.UnknownActingTeam);

            builder.Act(Side(team), e is FieldGoalAttempt { Made: true });
        }

        var possessions = builder.Build();
        var home = possessions.Count(p => p.Side == Sides.Home);

        if (Math.Abs(home - (possessions.Length - home)) > MaximumPossessionDifference)
            return Rejected(game, RejectionReason.UnbalancedPossessions);

        return Observed(game, RejectionReason.NotSet, possessions);
    }

    private static ObservedGame Rejected(Game game, RejectionReason reason) => Observed(game, reason, []);

    private static ObservedGame Observed(Game game, RejectionReason rejection, Possession[] possessions) =>
        new(game.SourceId, game.ScheduledStartTimeUtc, game.TeamA.Team.Id, game.TeamB.Team.Id, rejection, possessions);

    private static bool IsAction(Event e) =>
        e is FieldGoalAttempt or Turnover or FreeThrow { FreeThrowType: FreeThrowType.Regular };

    private static TeamType ActingTeam(Event e, Dictionary<long, TeamType> teams) => e switch
    {
        FieldGoalAttempt shot => TeamOf(shot.Participants, ParticipationType.ShotBy, teams),
        Turnover { Participants.Length: 0 } turnover => turnover.TeamInPossession,
        Turnover turnover => TeamOf(turnover.Participants, ParticipationType.TurnoverCommittedBy, teams),
        FreeThrow freeThrow => teams.GetValueOrDefault(freeThrow.ShooterId),
        _ => throw new ArgumentOutOfRangeException(nameof(e), e.EventType, "Not an action"),
    };

    private static TeamType TeamOf(Participant[]? participants, ParticipationType role, Dictionary<long, TeamType> teams)
    {
        var participant = participants?.FirstOrDefault(p => p.Type == role);
        return participant is null ? TeamType.NotSet : teams.GetValueOrDefault(participant.ParticipantId);
    }

    private static int Side(TeamType team) => team switch
    {
        TeamType.HomeOrA => Sides.Home,
        TeamType.AwayOrB => Sides.Away,
        _ => throw new ArgumentOutOfRangeException(nameof(team), team, "Not a side"),
    };

    private static Dictionary<long, TeamType> TeamsByPlayer(Game game) =>
        game.TeamA.Players.Select(p => (p.Player.Id, TeamType.HomeOrA))
            .Concat(game.TeamB.Players.Select(p => (p.Player.Id, TeamType.AwayOrB)))
            .ToDictionary();
}
