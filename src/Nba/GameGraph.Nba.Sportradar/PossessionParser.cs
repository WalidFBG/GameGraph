using System.Globalization;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Sportradar;

internal sealed class PossessionParser(Dictionary<long, TeamType> teams, Dictionary<long, int> slots, int[][] active)
{
    private const int TakeFoulFreeThrows = 1;

    private readonly List<Possession> _possessions = [];
    private readonly List<ObservedAction> _actions = [];
    private readonly Dictionary<string, AwardedKind> _foulsWithoutFreeThrowsInTheTree = [];
    private readonly Dictionary<string, int> _takeFoulers = [];
    private int? _team;
    private int _period;
    private int? _lastClock;
    private OpenAction? _open;
    private ObservedRebound? _reboundAtZero;
    private readonly List<AwardedFreeThrow> _awarded = [];
    private readonly List<int> _timeouts = [];
    private readonly LineupParser _lineups = new(teams, slots, active);

    public int PossessionsCutByPeriodEnd { get; private set; }

    public void Read(Event e)
    {
        if (e is not LineupChange)
            SettleMissedLastFreeThrow();

        if (_reboundAtZero is { } atZero && e is FieldGoalAttempt or Turnover or Foul { FoulType: FoulType.Shooting or FoulType.Personal })
        {
            _reboundAtZero = null;
            Finish(atZero);
        }

        switch (e)
        {
            case { EventType: EventType.TeamTimeout }:
                _timeouts.Add(Side(e.TeamInPossession));
                break;
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
                _actions.Add(new ObservedTurnover(ActionClock(turnover), TurnoverPlayer(turnover), turnover.TurnoverType == TurnoverType.OffensiveFoul, SlotIfAny(turnover.Participants, ParticipationType.StolenBy)) { AwardedBefore = TakeAwarded(), Before = TakeMoment() });
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

        _lineups.Read(e);
    }

    public Roster BuildRoster() => _lineups.Build();

    public Possession[] Build()
    {
        EndPeriod();
        return _possessions.ToArray();
    }

    private void EndPeriod()
    {
        SettleMissedLastFreeThrow();
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

        ClosePossession(ballAwarded: false);

        if (_awarded.Count > 0)
            _possessions[^1] = LastPossessionOfThePeriod(RejectionReason.UnmatchedFreeThrow) with { AwardedAfter = TakeAwarded() };

        if (_timeouts.Count > 0 || _lineups.HasChanges)
            _possessions[^1] = LastPossessionOfThePeriod(RejectionReason.UnmatchedMoment) with { After = TakeMoment() };
    }

    private Possession LastPossessionOfThePeriod(RejectionReason otherwise)
    {
        if (_possessions.Count == 0 || _possessions[^1].Period != _period)
            throw new RejectedGameException(otherwise);

        return _possessions[^1];
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
        _open = new OpenAction(ActionClock(shot), points, shot.Made ? Phase.AndOneWindow : Phase.Rebound, TakeAwarded(), TakeMoment()) { Made = shot.Made, Shooter = SlotOf(shot.Participants, ParticipationType.ShotBy), Assister = SlotIfAny(shot.Participants, ParticipationType.AssistedBy), Blocker = SlotIfAny(shot.Participants, ParticipationType.BlockedBy) };
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
                _foulsWithoutFreeThrowsInTheTree[foul.SourceId] = AwardedKind.Unclassified;
                break;
        }
    }

    private void ShootingFoul(Foul foul)
    {
        var drawn = TeamOf(foul.Participants, ParticipationType.FoulDrawnBy);

        if (_open is { Phase: Phase.AndOneWindow } shot && foul.FreeThrowsAwarded == 1 && drawn == _team)
        {
            shot.Fouler = SlotOf(foul.Participants, ParticipationType.FoulCommittedBy);
            shot.Expect(1);
            return;
        }

        CloseAndOneWindow();
        RequireNoOpenAction();

        if (foul.FreeThrowsAwarded is not (2 or 3))
            throw new RejectedGameException(RejectionReason.UnexpectedFoul);

        StartAction(drawn);
        _open = new OpenAction(ActionClock(foul), foul.FreeThrowsAwarded, Phase.FreeThrows, TakeAwarded(), TakeMoment()) { Fouler = SlotOf(foul.Participants, ParticipationType.FoulCommittedBy), Shooter = SlotOf(foul.Participants, ParticipationType.FoulDrawnBy) };
        _open.Expect(foul.FreeThrowsAwarded);
    }

    private void PersonalFoul(Foul foul)
    {
        CloseAndOneWindow();

        if (foul.FreeThrowsAwarded == TakeFoulFreeThrows)
        {
            _foulsWithoutFreeThrowsInTheTree[foul.SourceId] = AwardedKind.TakeFoul;
            _takeFoulers[foul.SourceId] = SlotOf(foul.Participants, ParticipationType.FoulCommittedBy);
            return;
        }

        var committed = TeamOf(foul.Participants, ParticipationType.FoulCommittedBy);
        var drawn = TeamOf(foul.Participants, ParticipationType.FoulDrawnBy);

        if (committed == drawn)
            throw new RejectedGameException(RejectionReason.UnexpectedFoul);

        if (_open is { Phase: Phase.Rebound })
            Finish(new ObservedRebound(drawn == _team, Rebounder: null));

        if (committed == Offense(drawn))
            return;

        RequireNoOpenAction();
        StartAction(drawn);

        var fouler = SlotOf(foul.Participants, ParticipationType.FoulCommittedBy);
        var fouled = SlotOf(foul.Participants, ParticipationType.FoulDrawnBy);

        switch (foul.FreeThrowsAwarded)
        {
            case 0:
                _actions.Add(new ObservedFoul(ActionClock(foul), fouler, fouled, [], null) { AwardedBefore = TakeAwarded(), Before = TakeMoment() });
                break;
            case FreeThrows.Bonus:
                _open = new OpenAction(ActionClock(foul), 0, Phase.FreeThrows, TakeAwarded(), TakeMoment()) { Fouler = fouler, FouledPlayer = fouled };
                _open.Expect(FreeThrows.Bonus);
                break;
            default:
                throw new RejectedGameException(RejectionReason.UnexpectedFoul);
        }
    }

    private void FreeThrow(FreeThrow freeThrow)
    {
        if (AwardedKindOf(freeThrow) is { } kind)
        {
            _awarded.Add(new AwardedFreeThrow(Side(teams.GetValueOrDefault(freeThrow.ShooterId)), freeThrow.Made, kind, slots[freeThrow.ShooterId], kind == AwardedKind.TakeFoul ? _takeFoulers[freeThrow.FoulId] : null));
            return;
        }

        CloseAndOneWindow();

        if (_open is not { Phase: Phase.FreeThrows } open || Side(teams.GetValueOrDefault(freeThrow.ShooterId)) != _team)
            throw new RejectedGameException(RejectionReason.UnmatchedFreeThrow);

        if (open.FreeThrows.Count + 1 == open.ExpectedFreeThrows && !freeThrow.Made)
        {
            open.Phase = Phase.MissedLastFreeThrow;
            return;
        }

        open.FreeThrows.Add(new ObservedFreeThrow(freeThrow.Made, TakeMoment()));

        if (open.FreeThrows.Count == open.ExpectedFreeThrows)
            Finish(null);
    }

    private void SettleMissedLastFreeThrow()
    {
        if (_open is not { Phase: Phase.MissedLastFreeThrow } open)
            return;

        open.FreeThrows.Add(new ObservedFreeThrow(false, TakeMoment()));
        open.Phase = Phase.Rebound;
    }

    private AwardedKind? AwardedKindOf(FreeThrow freeThrow) => freeThrow.FreeThrowType switch
    {
        FreeThrowType.Technical => AwardedKind.Technical,
        FreeThrowType.Flagrant => AwardedKind.Flagrant,
        FreeThrowType.ClearPath => AwardedKind.ClearPath,
        FreeThrowType.NotSet => AwardedKind.Unclassified,
        FreeThrowType.Regular => _foulsWithoutFreeThrowsInTheTree.TryGetValue(freeThrow.FoulId, out var kind) ? kind : null,
        _ => throw new ArgumentOutOfRangeException(nameof(freeThrow), freeThrow.FreeThrowType, "Not a free-throw type"),
    };

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

        var observed = new ObservedRebound(offensive, rebound.Catcher is null ? null : slots[rebound.Catcher.ParticipantId]);

        if (_open.Clock == 0)
            _reboundAtZero ??= observed;
        else
            Finish(observed);
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

    private void Finish(ObservedRebound? rebound)
    {
        var open = _open ?? throw new InvalidOperationException("No action to finish");

        _actions.Add(open.Points == 0
            ? new ObservedFoul(open.Clock, open.Fouler ?? throw new InvalidOperationException("A non-shooting foul has its fouler"), open.FouledPlayer, open.FreeThrows.ToArray(), rebound) { AwardedBefore = open.AwardedBefore, Before = open.Before }
            : new ObservedShot(open.Clock, open.Shooter, open.Points, open.Fouler, open.Made, open.Assister, open.Blocker, open.FreeThrows.ToArray(), rebound) { AwardedBefore = open.AwardedBefore, Before = open.Before });
        _open = null;
    }

    private void StartAction(int team)
    {
        if (team == _team)
        {
            if (_actions.Count > 0 && _actions[^1].EndsPossession)
            {
                RequireBallAwardedTo(team, RejectionReason.SameTeamTwice);
                ClosePossession(ballAwarded: true);
                _team = team;
            }

            return;
        }

        var cutShort = _team is not null && !_actions[^1].EndsPossession;

        if (cutShort)
            RequireBallAwardedTo(team, RejectionReason.PossessionCutShort);

        ClosePossession(ballAwarded: cutShort);
        _team = team;
    }

    private void RequireBallAwardedTo(int team, RejectionReason otherwise)
    {
        if (!_awarded.Any(f => f.Team == team && f.MovesTheBall))
            throw new RejectedGameException(otherwise);
    }

    private void ClosePossession(bool ballAwarded)
    {
        if (_team is not { } team)
            return;

        _possessions.Add(new Possession(team, _period, _actions.ToArray()) { AwardedAfter = ballAwarded ? TakeAwarded() : [], BallAwardedAfter = ballAwarded });
        _actions.Clear();
        _team = null;
    }

    private ObservedMoment TakeMoment()
    {
        var timeouts = _timeouts.ToArray();
        _timeouts.Clear();
        return new ObservedMoment(timeouts, _lineups.Take());
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

    private int? TurnoverPlayer(Turnover turnover) => SlotIfAny(turnover.Participants, ParticipationType.TurnoverCommittedBy);

    private int? SlotIfAny(Participant[]? participants, ParticipationType role) =>
        participants?.Any(p => p.Type == role) == true ? SlotOf(participants, role) : null;

    private int TeamOf(Participant[]? participants, ParticipationType role)
    {
        var participant = participants?.FirstOrDefault(p => p.Type == role)
            ?? throw new RejectedGameException(RejectionReason.UnknownActingTeam);

        return Side(teams.GetValueOrDefault(participant.ParticipantId));
    }

    private int SlotOf(Participant[]? participants, ParticipationType role)
    {
        TeamOf(participants, role);
        return slots[participants!.First(p => p.Type == role).ParticipantId];
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
        MissedLastFreeThrow = 4,
    }

    private sealed class OpenAction(int clock, int points, Phase phase, AwardedFreeThrow[] awardedBefore, ObservedMoment before)
    {
        public int Clock { get; } = clock;

        public AwardedFreeThrow[] AwardedBefore { get; } = awardedBefore;

        public ObservedMoment Before { get; } = before;

        public int Points { get; } = points;

        public Phase Phase { get; set; } = phase;

        public bool Made { get; init; }

        public int Shooter { get; init; }

        public int? Assister { get; init; }

        public int? Blocker { get; init; }

        public int? Fouler { get; set; }

        public int FouledPlayer { get; init; }

        public int ExpectedFreeThrows { get; private set; }

        public List<ObservedFreeThrow> FreeThrows { get; } = [];

        public void Expect(int freeThrows)
        {
            ExpectedFreeThrows = freeThrows;
            Phase = Phase.FreeThrows;
        }
    }
}
