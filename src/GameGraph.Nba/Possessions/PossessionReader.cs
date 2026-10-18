using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Possessions;

public static class PossessionReader
{
    public const int MaximumPossessionDifference = 4;

    public static PossessionReading Read(Game game)
    {
        if (game.ValidationSummary.HasInvalidEvents)
            return PossessionReading.Rejected(RejectionReason.InvalidEvents);

        var teams = TeamsByPlayer(game);
        var builder = new PossessionBuilder();

        foreach (var e in game.Events)
        {
            if (e.EventType == EventType.EndPeriod)
            {
                builder.End();
                continue;
            }

            if (!IsAction(e))
                continue;

            var team = ActingTeam(e, teams);

            if (team == TeamType.NotSet)
                return PossessionReading.Rejected(RejectionReason.UnknownActingTeam);

            builder.Act(team, e is FieldGoalAttempt { Made: true });
        }

        var possessions = builder.Build();
        var home = possessions.Count(p => p.Team == TeamType.HomeOrA);

        if (Math.Abs(home - (possessions.Length - home)) > MaximumPossessionDifference)
            return PossessionReading.Rejected(RejectionReason.UnbalancedPossessions);

        return PossessionReading.Accepted(possessions);
    }

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

    private static Dictionary<long, TeamType> TeamsByPlayer(Game game) =>
        game.TeamA.Players.Select(p => (p.Player.Id, TeamType.HomeOrA))
            .Concat(game.TeamB.Players.Select(p => (p.Player.Id, TeamType.AwayOrB)))
            .ToDictionary();
}
