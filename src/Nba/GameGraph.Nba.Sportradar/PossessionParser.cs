using System.Globalization;
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
    private int? _lastClock;
    private OpenAction? _open;
    private bool? _reboundAtZero;
    private readonly List<AwardedFreeThrow> _awarded = [];

    public int PossessionsCutByPeriodEnd { get; private set; }

    public void Read(Event e)
    {
        if (_reboundAtZero is { } offensive && e is FieldGoalAttempt or Turnover or Foul { FoulType: FoulType.Shooting or FoulType.Personal })
        {
            _reboundAtZero = null;
            Finish(offensive);
        }

        switch (e)
        {
            case { EventType: EventType.EndPeriod }:
                EndPeriod();
                _period++;
                _lastClock = null;
                break;
            case FieldGoalAttempt shot:
                Shot(shot);
                break;
            case Turnover turnover:
                CloseAndOneWindow();
                RequireNoOpenAction();
                StartAction(TurnoverTeam(turnover));
                _actions.Add(new ObservedTurnover(ActionClock(turnover)) { AwardedBefore = TakeAwarded() });
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

        if (_open is { Phase: Phase.Rebound, Clock: 0 })
        {
            _reboundAtZero = null;
            Finish(null);
        }

        if (_open is not null)
            throw new RejectedGameException(RejectionReason.PeriodEndsMidAction);

        if (_team is not null && !_actions[^1].EndsPossession)
            PossessionsCutByPeriodEnd++;

        ClosePossession();

        if (_awarded.Count == 0)
            return;

        if (_possessions.Count == 0 || _possessions[^1].Period != _period)
            throw new RejectedGameException(RejectionReason.UnmatchedFreeThrow);

        _possessions[^1] = _possessions[^1] with { AwardedAfter = TakeAwarded() };
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
        _open = new OpenAction(ActionClock(shot), points, shot.Made ? Phase.AndOneWindow : Phase.Rebound, TakeAwarded()) { Made = shot.Made };
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
        _open = new OpenAction(ActionClock(foul), foul.FreeThrowsAwarded, Phase.FreeThrows, TakeAwarded()) { Fouled = true };
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
                _actions.Add(new ObservedFoul(ActionClock(foul), [], null) { AwardedBefore = TakeAwarded() });
                break;
            case NbaTransitions.BonusFreeThrows:
                _open = new OpenAction(ActionClock(foul), 0, Phase.FreeThrows, TakeAwarded());
                _open.Expect(NbaTransitions.BonusFreeThrows);
                break;
            default:
                throw new RejectedGameException(RejectionReason.UnexpectedFoul);
        }
    }

    private void FreeThrow(FreeThrow freeThrow)
    {
        if (freeThrow.FreeThrowType != FreeThrowType.Regular || _foulsWithoutFreeThrowsInTheTree.Contains(freeThrow.FoulId))
        {
            _awarded.Add(new AwardedFreeThrow(Side(teams.GetValueOrDefault(freeThrow.ShooterId)), freeThrow.Made));
            return;
        }

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

        if (_open.Clock == 0)
            _reboundAtZero ??= offensive;
        else
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
            ? new ObservedFoul(open.Clock, open.FreeThrows.ToArray(), offensiveRebound) { AwardedBefore = open.AwardedBefore }
            : new ObservedShot(open.Clock, open.Points, open.Fouled, open.Made, open.FreeThrows.ToArray(), offensiveRebound) { AwardedBefore = open.AwardedBefore });
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

    private AwardedFreeThrow[] TakeAwarded()
    {
        var awarded = _awarded.ToArray();
        _awarded.Clear();
        return awarded;
    }

    private int ActionClock(Event e)
    {
        var clock = e.GameClock ?? throw new InvalidDataException($"Event {e.SourceId} has no game clock");
        var parts = clock.MinutesIntoPeriod.Split(':');
        var seconds = int.Parse(parts[0], CultureInfo.InvariantCulture) * 60 + int.Parse(parts[1], CultureInfo.InvariantCulture);

        if (seconds > _lastClock)
            throw new RejectedGameException(RejectionReason.ClockRunsBackwards);

        _lastClock = seconds;
        return seconds;
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

    private sealed class OpenAction(int clock, int points, Phase phase, AwardedFreeThrow[] awardedBefore)
    {
        public int Clock { get; } = clock;

        public AwardedFreeThrow[] AwardedBefore { get; } = awardedBefore;

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
