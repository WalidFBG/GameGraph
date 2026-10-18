using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Sportradar;

internal sealed class PossessionParser(Dictionary<long, TeamType> teams)
{
    private const int TakeFoulFreeThrows = 1;

    private readonly List<Possession> _possessions = [];
    private readonly List<ObservedAction> _actions = [];
    private readonly HashSet<string> _foulsWithoutFreeThrowsInTheTree = [];
    private int? _team;
    private int _period;
    private OpenAction? _open;

    public int PossessionsCutByPeriodEnd { get; private set; }

    public void Read(Event e)
    {
        switch (e)
        {
            case { EventType: EventType.EndPeriod }:
                EndPeriod();
                _period++;
                break;
            case FieldGoalAttempt shot:
                Shot(shot);
                break;
            case Turnover turnover:
                CloseAndOneWindow();
                RequireNoOpenAction();
                StartAction(TurnoverTeam(turnover));
                _actions.Add(new ObservedTurnover());
                break;
            case Foul foul:
                Foul(foul);
                break;
            case FreeThrow freeThrow:
                FreeThrow(freeThrow);
                break;
            case Rebound rebound:
                Rebound(rebound);
                break;
        }
    }

    public Possession[] Build()
    {
        EndPeriod();
        return _possessions.ToArray();
    }

    private void EndPeriod()
    {
        CloseAndOneWindow();

        if (_open is not null || (_team is not null && !_actions[^1].EndsPossession))
        {
            PossessionsCutByPeriodEnd++;
            _actions.Clear();
            _open = null;
            _team = null;
            return;
        }

        ClosePossession();
    }

    private void Shot(FieldGoalAttempt shot)
    {
        CloseAndOneWindow();
        RequireNoOpenAction();

        var points = shot.FieldGoalType switch
        {
            FieldGoalType.TwoPointer => 2,
            FieldGoalType.ThreePointer => 3,
            _ => throw new RejectedGameException(RejectionReason.UnknownShotType),
        };

        StartAction(TeamOf(shot.Participants, ParticipationType.ShotBy));
        _open = new OpenAction(points, shot.Made ? Phase.AndOneWindow : Phase.Rebound) { Made = shot.Made };
    }

    private void Foul(Foul foul)
    {
        switch (foul.FoulType)
        {
            case FoulType.Shooting:
                ShootingFoul(foul);
                break;
            case FoulType.Personal:
                PersonalFoul(foul);
                break;
            case FoulType.NotSet:
                _foulsWithoutFreeThrowsInTheTree.Add(foul.SourceId);
                break;
        }
    }

    private void ShootingFoul(Foul foul)
    {
        var drawn = TeamOf(foul.Participants, ParticipationType.FoulDrawnBy);

        if (_open is { Phase: Phase.AndOneWindow } shot && foul.FreeThrowsAwarded == 1 && drawn == _team)
        {
            shot.Fouled = true;
            shot.Expect(1);
            return;
        }

        CloseAndOneWindow();
        RequireNoOpenAction();

        if (foul.FreeThrowsAwarded is not (2 or 3))
            throw new RejectedGameException(RejectionReason.UnexpectedFoul);

        StartAction(drawn);
        _open = new OpenAction(foul.FreeThrowsAwarded, Phase.FreeThrows) { Fouled = true };
        _open.Expect(foul.FreeThrowsAwarded);
    }

    private void PersonalFoul(Foul foul)
    {
        CloseAndOneWindow();

        if (foul.FreeThrowsAwarded == TakeFoulFreeThrows)
        {
            _foulsWithoutFreeThrowsInTheTree.Add(foul.SourceId);
            return;
        }

        var committed = TeamOf(foul.Participants, ParticipationType.FoulCommittedBy);
        var drawn = TeamOf(foul.Participants, ParticipationType.FoulDrawnBy);

        if (committed == drawn)
            throw new RejectedGameException(RejectionReason.UnexpectedFoul);

        if (_open is { Phase: Phase.Rebound })
            Finish(drawn == _team);

        if (committed == Offense(drawn))
            return;

        RequireNoOpenAction();
        StartAction(drawn);

        switch (foul.FreeThrowsAwarded)
        {
            case 0:
                _actions.Add(new ObservedFoul([], null));
                break;
            case NbaTransitions.BonusFreeThrows:
                _open = new OpenAction(0, Phase.FreeThrows);
                _open.Expect(NbaTransitions.BonusFreeThrows);
                break;
            default:
                throw new RejectedGameException(RejectionReason.UnexpectedFoul);
        }
    }

    private void FreeThrow(FreeThrow freeThrow)
    {
        if (freeThrow.FreeThrowType != FreeThrowType.Regular || _foulsWithoutFreeThrowsInTheTree.Contains(freeThrow.FoulId))
            return;

        CloseAndOneWindow();

        if (_open is not { Phase: Phase.FreeThrows } open || Side(teams.GetValueOrDefault(freeThrow.ShooterId)) != _team)
            throw new RejectedGameException(RejectionReason.UnmatchedFreeThrow);

        open.FreeThrows.Add(freeThrow.Made);

        if (open.FreeThrows.Count < open.ExpectedFreeThrows)
            return;

        if (freeThrow.Made)
            Finish(null);
        else
            open.Phase = Phase.Rebound;
    }

    private void Rebound(Rebound rebound)
    {
        if (_open is not { Phase: Phase.Rebound })
            return;

        var offensive = rebound.ReboundType switch
        {
            ReboundType.Offensive => true,
            ReboundType.Defensive => false,
            _ => throw new RejectedGameException(RejectionReason.UnknownReboundType),
        };

        Finish(offensive);
    }

    private void CloseAndOneWindow()
    {
        if (_open is { Phase: Phase.AndOneWindow })
            Finish(null);
    }

    private void RequireNoOpenAction()
    {
        if (_open is not null)
            throw new RejectedGameException(RejectionReason.InterruptedAction);
    }

    private void Finish(bool? offensiveRebound)
    {
        var open = _open ?? throw new InvalidOperationException("No action to finish");

        _actions.Add(open.Points == 0
            ? new ObservedFoul(open.FreeThrows.ToArray(), offensiveRebound)
            : new ObservedShot(open.Points, open.Fouled, open.Made, open.FreeThrows.ToArray(), offensiveRebound));
        _open = null;
    }

    private void StartAction(int team)
    {
        if (team == _team)
        {
            if (_actions.Count > 0 && _actions[^1].EndsPossession)
                throw new RejectedGameException(RejectionReason.SameTeamTwice);

            return;
        }

        if (_team is not null && !_actions[^1].EndsPossession)
            throw new RejectedGameException(RejectionReason.PossessionCutShort);

        ClosePossession();
        _team = team;
    }

    private void ClosePossession()
    {
        if (_team is not { } team)
            return;

        _possessions.Add(new Possession(team, _period, _actions.ToArray()));
        _actions.Clear();
        _team = null;
    }

    private int Offense(int drawn)
    {
        if (_team is not { } team)
            return drawn;

        return _actions.Count > 0 && _actions[^1].EndsPossession ? Sides.Opponent(team) : team;
    }

    private int TurnoverTeam(Turnover turnover) =>
        turnover.Participants is { Length: > 0 }
            ? TeamOf(turnover.Participants, ParticipationType.TurnoverCommittedBy)
            : Side(turnover.TeamInPossession);

    private int TeamOf(Participant[]? participants, ParticipationType role)
    {
        var participant = participants?.FirstOrDefault(p => p.Type == role)
            ?? throw new RejectedGameException(RejectionReason.UnknownActingTeam);

        return Side(teams.GetValueOrDefault(participant.ParticipantId));
    }

    private static int Side(TeamType team) => team switch
    {
        TeamType.HomeOrA => Sides.Home,
        TeamType.AwayOrB => Sides.Away,
        _ => throw new RejectedGameException(RejectionReason.UnknownActingTeam),
    };

    private enum Phase
    {
        NotSet = 0,
        AndOneWindow = 1,
        FreeThrows = 2,
        Rebound = 3,
    }

    private sealed class OpenAction(int points, Phase phase)
    {
        public int Points { get; } = points;

        public Phase Phase { get; set; } = phase;

        public bool Made { get; init; }

        public bool Fouled { get; set; }

        public int ExpectedFreeThrows { get; private set; }

        public List<bool> FreeThrows { get; } = [];

        public void Expect(int freeThrows)
        {
            ExpectedFreeThrows = freeThrows;
            Phase = Phase.FreeThrows;
        }
    }
}
