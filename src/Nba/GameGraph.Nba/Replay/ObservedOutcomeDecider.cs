using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Replay;

public sealed class ObservedOutcomeDecider : IOutcomeDecider<NbaState>
{
    private const double UnreadPointsPerPossession = 0;

    private readonly AcceptedGame _game;
    private readonly Dictionary<NodeSpec<NbaState>, Func<NbaState, int>> _readers;
    private int _possession;
    private int _action = -1;
    private int _freeThrow;
    private bool _reboundRead;
    private bool _afterRead;
    private bool _nextBeforeRead;
    private ObservedStoppage? _stoppage;
    private AwardedFreeThrow? _awarded;

    public ObservedOutcomeDecider(AcceptedGame game)
    {
        _game = game;
        _readers = new()
        {
            [NbaNodes.Starters] = s => Chosen(NbaNodes.Starters, s, game.Roster.Starters[s.LineupTeam]),
            [NbaNodes.JumpBall] = s => Number(NextPossession(s).TeamInPossession == Sides.Home),
            [NbaNodes.OffenceTimeout] = s => Stoppage(s).Called(s.TeamInPossession),
            [NbaNodes.DefenceTimeout] = s => Stoppage(s).Called(Sides.Opponent(s.TeamInPossession)),
            [NbaNodes.Substitutions] = s => Stoppage(s).Teams(s.TeamInPossession),
            [NbaNodes.SubstitutionCount] = s => Chosen(NbaNodes.SubstitutionCount, s, [ForcedAmong(s, Stoppage(s).Change(s.LineupTeam).Off).Length]),
            [NbaNodes.SubstitutedOff] = s => Chosen(NbaNodes.SubstitutedOff, s, [.. Stoppage(s).Change(s.LineupTeam).Off.Except(s.Leaving.Take(s.ForcedOff))]),
            [NbaNodes.SubstitutedOn] = s => Chosen(NbaNodes.SubstitutedOn, s, Stoppage(s).TakeChange(s.LineupTeam).On),
            [NbaNodes.AwardedFreeThrow] = NextAwarded,
            [NbaNodes.AwardedShooter] = s => Chosen(NbaNodes.AwardedShooter, s, [Awarded().Shooter]),
            [NbaNodes.AwardedFouler] = s => Chosen(NbaNodes.AwardedFouler, s, [Awarded().Fouler ?? throw Disagree("The rules ask for the fouler of an awarded free throw that has none")]),
            [NbaNodes.AwardedFreeThrowMade] = _ => Number(Awarded().Made),
            [NbaNodes.PeriodRunsOut] = PeriodRunsOut,
            [NbaNodes.ActionType] = s => ActionTypes.Category(NextAction(s).Type),
            [NbaNodes.ActionTime] = s => ActionTime(s, Current<ObservedAction>().Clock),
            [NbaNodes.Shooter] = s => Chosen(NbaNodes.Shooter, s, [Current<ObservedShot>().Shooter]),
            [NbaNodes.FouledOnShot] = _ => Number(Current<ObservedShot>().Fouled),
            [NbaNodes.Fouler] = s => Chosen(NbaNodes.Fouler, s, [Fouler()]),
            [NbaNodes.ShotMade] = _ => Number(Current<ObservedShot>().Made),
            [NbaNodes.Assisted] = _ => Number(Current<ObservedShot>().Assister is not null),
            [NbaNodes.Assister] = s => Chosen(NbaNodes.Assister, s, [Current<ObservedShot>().Assister ?? throw Disagree("The rules ask for the assister of an unassisted shot")]),
            [NbaNodes.Blocked] = _ => Number(Current<ObservedShot>().Blocker is not null),
            [NbaNodes.Blocker] = s => Chosen(NbaNodes.Blocker, s, [Current<ObservedShot>().Blocker ?? throw Disagree("The rules ask for the blocker of an unblocked shot")]),
            [NbaNodes.FouledPlayer] = s => Chosen(NbaNodes.FouledPlayer, s, [Current<ObservedFoul>().FouledPlayer]),
            [NbaNodes.InBonus] = _ => Number(Current<ObservedFoul>().InBonus),
            [NbaNodes.FreeThrowMade] = _ => FreeThrowMade(),
            [NbaNodes.OffensiveRebound] = _ => Number(ReadRebound().Offensive),
            [NbaNodes.TeamRebound] = _ => Number(Rebound().ByTeam),
            [NbaNodes.Rebounder] = s => Chosen(NbaNodes.Rebounder, s, [Rebound().Rebounder ?? throw Disagree("The rules ask for the rebounder of a team rebound")]),
            [NbaNodes.TurnoverKind] = _ => Current<ObservedTurnover>().Kind,
            [NbaNodes.TurnedOverBy] = s => Chosen(NbaNodes.TurnedOverBy, s, [Current<ObservedTurnover>().Player ?? throw Disagree("The rules ask for the player of a turnover that has none")]),
            [NbaNodes.Stolen] = _ => Number(Current<ObservedTurnover>().Stolen),
            [NbaNodes.Stealer] = s => Chosen(NbaNodes.Stealer, s, [Current<ObservedTurnover>().Stealer ?? throw Disagree("The rules ask for the stealer of a turnover that was not stolen")]),
        };

        var unread = NbaGameSpec.Instance.Nodes.Where(n => !_readers.ContainsKey(n)).Select(n => n.Name).ToArray();

        if (unread.Length > 0)
            throw new InvalidOperationException($"The replay reads no fact for {string.Join(", ", unread)}");
    }

    public int ActionTimes { get; private set; }

    public int ClampedActionTimes { get; private set; }

    public static bool Follows(AcceptedGame game)
    {
        var decider = new ObservedOutcomeDecider(game);
        var unread = new double[game.Players.Length];
        double[][] unreadShares = [.. Enum.GetValues<PlayerEvent>().Select(_ => unread)];
        var context = new GameContext(UnreadPointsPerPossession, UnreadPointsPerPossession, unreadShares, new PlayerRates(unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread, unread), unread, unread, unread, game.Roster.Active);
        var state = new NbaState(new EmptyIncidentCollector(), context);

        try
        {
            GameWalker.Walk(NbaGameSpec.Instance, state, decider, new EmptyDecisionCollector<NbaState>());
            decider.EnsureAllUsed();
            return true;
        }
        catch (RulesDisagreeWithDataException)
        {
            return false;
        }
    }

    public int Decide(NodeSpec<NbaState> node, NbaState state, ReadOnlySpan<double> features) => _readers[node](state);

    public void EnsureAllUsed()
    {
        if (_stoppage is not null)
            throw Disagree("The game ended inside a stoppage");

        if (Next() is { } next)
            throw Disagree($"The game ended before action {next.Action} of possession {next.Possession}");

        if (_action >= 0)
            RequireActionRead();

        if (!_afterRead && HasAfter(_game.Possessions[_possession]))
            throw Disagree("The game ended before the stoppage after its last possession");
    }

    private ObservedAction CurrentAction =>
        _action >= 0 ? _game.Possessions[_possession].Actions[_action] : throw Disagree("The rules ask about an action before the game's first");

    private T Current<T>()
        where T : ObservedAction =>
        CurrentAction as T ?? throw Disagree($"The rules ask about a {typeof(T).Name}, the data has a {CurrentAction.GetType().Name}");

    private (int Possession, int Action)? Next()
    {
        if (_action + 1 < _game.Possessions[_possession].Actions.Length)
            return (_possession, _action + 1);

        return _possession + 1 < _game.Possessions.Length ? (_possession + 1, 0) : null;
    }

    private Possession NextPossession(NbaState state)
    {
        var next = Next() ?? throw Disagree("The rules ask for a possession after the game's last");
        var possession = _game.Possessions[next.Possession];

        return possession.Period == state.Period
            ? possession
            : throw Disagree($"The rules ask for a possession in period {state.Period}, the data's next is in period {possession.Period}");
    }

    private ObservedAction NextAction(NbaState state)
    {
        var next = Next() ?? throw Disagree("The rules ask for an action after the game's last");
        var possession = _game.Possessions[next.Possession];

        if (_action >= 0)
            RequireActionRead();

        if (next.Possession != _possession && !_afterRead && HasAfter(_game.Possessions[_possession]))
            throw Disagree("The rules move to the next possession before the stoppage after this one");

        if (!_nextBeforeRead)
            throw Disagree("The rules ask for an action before its stoppage");

        if (possession.TeamInPossession != state.TeamInPossession || possession.Period != state.Period)
            throw Disagree($"The data's next action is {Sides.Name(possession.TeamInPossession)}'s in period {possession.Period}, the rules ask for {Sides.Name(state.TeamInPossession)}'s in period {state.Period}");

        if (next.Possession != _possession)
            _afterRead = false;

        (_possession, _action) = next;
        _freeThrow = 0;
        _reboundRead = false;
        _nextBeforeRead = false;

        return CurrentAction;
    }

    private void RequireActionRead()
    {
        if (!IsActionRead())
            throw Disagree("The rules left an action's free throws or rebound unread");
    }

    private bool IsActionRead() => _freeThrow == FreeThrowsOf(CurrentAction).Length && (_reboundRead || ReboundOf(CurrentAction) is null);

    private ObservedStoppage Stoppage(NbaState state) =>
        _stoppage ??= state.Moment switch
        {
            Moment.FreeThrow => FreeThrowStoppage(),
            Moment.Play => PlayStoppage(state),
            _ => throw new InvalidOperationException($"A stoppage is asked about outside one: {state.Moment}"),
        };

    private ObservedStoppage FreeThrowStoppage()
    {
        var throws = FreeThrowsOf(CurrentAction);

        return _freeThrow < throws.Length
            ? new ObservedStoppage(throws[_freeThrow].Before, [])
            : throw Disagree("The rules ask for a free throw the action does not have");
    }

    private ObservedStoppage PlayStoppage(NbaState state)
    {
        if (_action >= 0)
        {
            var possession = _game.Possessions[_possession];

            if (_action == possession.Actions.Length - 1 && !_afterRead && HasAfter(possession))
            {
                var last = possession.Actions[^1];
                var team = last.EndsPossession && !last.PeriodEndedBeforeRebound ? Sides.Opponent(possession.TeamInPossession) : possession.TeamInPossession;

                if (team != state.TeamInPossession || possession.Period != state.Period)
                    throw Disagree($"The stoppage after a possession is {Sides.Name(team)}'s in period {possession.Period}, the rules are at {Sides.Name(state.TeamInPossession)}'s in period {state.Period}");

                _afterRead = true;
                return new ObservedStoppage(possession.After, possession.AwardedAfter);
            }
        }

        if (_nextBeforeRead || (_action >= 0 && !IsActionRead()) || Next() is not { } next)
            return ObservedStoppage.Empty;

        var upcoming = _game.Possessions[next.Possession];

        if (upcoming.Period != state.Period || upcoming.TeamInPossession != state.TeamInPossession)
            return ObservedStoppage.Empty;

        var action = upcoming.Actions[next.Action];

        _nextBeforeRead = true;
        return new ObservedStoppage(action.Before, action.AwardedBefore);
    }

    private int NextAwarded(NbaState state)
    {
        var stoppage = _stoppage ?? throw new InvalidOperationException("Awarded free throws are asked about outside a stoppage");

        if (stoppage.NextAwarded() is { } awarded)
        {
            _awarded = awarded;
            return AwardedFreeThrows.CategoryOf(awarded.Kind, awarded.Team != state.TeamInPossession)
                ?? throw Disagree($"The rules have no category for a {awarded.Kind} free throw to the {(awarded.Team != state.TeamInPossession ? "defence" : "offence")}");
        }

        CloseStoppage();
        return AwardedFreeThrows.None;
    }

    private AwardedFreeThrow Awarded() => _awarded ?? throw new InvalidOperationException("An awarded free throw is asked about before one was awarded");

    private int FreeThrowMade()
    {
        var throws = FreeThrowsOf(CurrentAction);

        if (_freeThrow >= throws.Length)
            throw Disagree("The rules ask for a free throw the action does not have");

        CloseStoppage();
        return Number(throws[_freeThrow++].Made);
    }

    private void CloseStoppage()
    {
        var stoppage = _stoppage ?? throw new InvalidOperationException("No stoppage to close");

        if (!stoppage.IsRead)
            throw Disagree("The rules left a stoppage's timeouts, substitutions or awarded free throws unread");

        _stoppage = null;
    }

    private int PeriodRunsOut(NbaState state)
    {
        if (state.LastMiss != Miss.NotSet)
            return Number(ReboundOf(CurrentAction) is null);

        return Number(Next() is not { } next || _game.Possessions[next.Possession].Period != state.Period);
    }

    private int Fouler() => CurrentAction switch
    {
        ObservedShot shot => shot.Fouler ?? throw Disagree("The rules ask for the fouler of a clean shot"),
        ObservedFoul foul => foul.Fouler,
        var other => throw Disagree($"The rules ask for the fouler of a {other.GetType().Name}"),
    };

    private ObservedRebound ReadRebound()
    {
        var rebound = Rebound();

        _reboundRead = true;
        return rebound;
    }

    private ObservedRebound Rebound() => ReboundOf(CurrentAction) ?? throw Disagree("The rules ask for a rebound the action does not have");

    private static ObservedFreeThrow[] FreeThrowsOf(ObservedAction action) => action switch
    {
        ObservedShot shot => shot.FreeThrows,
        ObservedFoul foul => foul.FreeThrows,
        _ => [],
    };

    private static ObservedRebound? ReboundOf(ObservedAction action) => action switch
    {
        ObservedShot shot => shot.Rebound,
        ObservedFoul foul => foul.Rebound,
        _ => null,
    };

    private static bool HasAfter(Possession possession) => possession.After.Timeouts.Length > 0 || possession.After.Substitutions.Length > 0 || possession.AwardedAfter.Length > 0;

    private int ActionTime(NbaState state, int clock)
    {
        var seconds = state.PeriodClock - clock;

        if (seconds < 0)
            throw Disagree($"An action at {clock} seconds left comes before the period clock's {state.PeriodClock}");

        ActionTimes++;

        if (seconds <= state.Bound)
            return seconds;

        ClampedActionTimes++;
        return state.Bound;
    }

    private static int[] ForcedAmong(NbaState state, int[] off)
    {
        var forced = state.Leaving.Take(state.ForcedOff).ToArray();

        return forced.All(off.Contains)
            ? off
            : throw Disagree($"[{string.Join(", ", forced)}] fouled out in period {state.Period} at {state.PeriodClock} seconds left, but the change takes off [{string.Join(", ", off)}]");
    }

    private static int Chosen(ChoiceNode<NbaState> node, NbaState state, int[] chosen) =>
        node.TryRowsOf(state, chosen, out var rows)
            ? rows
            : throw Disagree($"{node.Name} chose [{string.Join(", ", chosen)}] in period {state.Period} at {state.PeriodClock} seconds left, but they are not among its candidates");

    private static RulesDisagreeWithDataException Disagree(string message) => new(message);

    private static int Number(bool value) => value ? 1 : 0;
}
