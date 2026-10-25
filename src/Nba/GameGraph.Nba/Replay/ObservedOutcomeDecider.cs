using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Replay;

public sealed class ObservedOutcomeDecider : IOutcomeDecider<NbaState>
{
    private const double UnreadPointsPerPossession = 0;

    private readonly ObservedDecision[] _decisions;
    private int _next;

    public ObservedOutcomeDecider(Possession[] possessions)
    {
        var decisions = new List<ObservedDecision>();

        for (var i = 0; i < possessions.Length; i++)
        {
            var possession = possessions[i];

            if ((i == 0 || possessions[i - 1].Period != possession.Period) && (possession.Period == 0 || possession.Period >= Periods.Regulation))
                decisions.Add(new ObservedDecision(NbaNodes.JumpBall, _ => Number(possession.TeamInPossession == Sides.Home), Sides.Home, possession.Period));

            decisions.AddRange(Decisions(possession));
        }

        _decisions = [.. decisions];
    }

    public int ActionTimes { get; private set; }

    public int ClampedActionTimes { get; private set; }

    public static bool Follows(ObservedGame game)
    {
        var decider = new ObservedOutcomeDecider(game.Possessions);
        var unread = new double[game.Players.Length];
        var context = new GameContext(UnreadPointsPerPossession, UnreadPointsPerPossession, unread, unread);
        var state = new NbaState(new EmptyIncidentCollector(), context, game.Rotation);

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

    public int Decide(NodeSpec<NbaState> node, NbaState state, ReadOnlySpan<double> features)
    {
        if (node == NbaNodes.AwardedFreeThrow && !IsUpcoming(node, state))
            return AwardedFreeThrows.None;

        if ((node == NbaNodes.OffenceTimeout || node == NbaNodes.DefenceTimeout) && !IsUpcoming(node, state))
            return 0;

        if (node == NbaNodes.PeriodRunsOut)
            return Number(UpcomingIn(state.Period) is null);

        var decision = Upcoming() ?? throw new RulesDisagreeWithDataException($"The rules asked for {node.Name} after the game's last observed decision");

        if (decision.Node != node || decision.TeamInPossession != state.TeamInPossession || decision.Period != state.Period)
            throw new RulesDisagreeWithDataException($"Decision {_next}: the data has {decision.Node.Name} for {Sides.Name(decision.TeamInPossession)} in period {decision.Period}, the rules ask for {node.Name} for {Sides.Name(state.TeamInPossession)} in period {state.Period}");

        _next++;
        return decision.Outcome(state);
    }

    public void EnsureAllUsed()
    {
        if (_next != _decisions.Length)
            throw new RulesDisagreeWithDataException($"The game ended after {_next} of {_decisions.Length} observed decisions");
    }

    private ObservedDecision? Upcoming() => _next < _decisions.Length ? _decisions[_next] : null;

    private ObservedDecision? UpcomingIn(int period) => Upcoming() is { } next && next.Period == period ? next : null;

    private bool IsUpcoming(NodeSpec<NbaState> node, NbaState state) =>
        Upcoming() is { } next && next.Node == node && next.TeamInPossession == state.TeamInPossession && next.Period == state.Period;

    private int ActionTime(NbaState state, int clock)
    {
        var seconds = state.PeriodClock - clock;

        if (seconds < 0)
            throw new RulesDisagreeWithDataException($"An action at {clock} seconds left comes before the period clock's {state.PeriodClock}");

        ActionTimes++;

        if (seconds <= state.Bound)
            return seconds;

        ClampedActionTimes++;
        return state.Bound;
    }

    private IEnumerable<ObservedDecision> Decisions(Possession possession)
    {
        var team = possession.TeamInPossession;

        foreach (var action in possession.Actions)
        {
            foreach (var decision in Timeouts(action.TimeoutsBefore, team))
                yield return decision;

            foreach (var decision in Awarded(action.AwardedBefore, team))
                yield return decision;

            yield return Decision(NbaNodes.ActionType, ActionTypes.Category(action.Type));
            yield return new ObservedDecision(NbaNodes.ActionTime, s => ActionTime(s, action.Clock), team, possession.Period);

            switch (action)
            {
                case ObservedShot shot:
                    yield return new ObservedDecision(NbaNodes.Shooter, s => CandidateRow(NbaNodes.Shooter, s, shot.Shooter), team, possession.Period);
                    yield return Decision(NbaNodes.FouledOnShot, Number(shot.Fouled));
                    yield return Decision(NbaNodes.ShotMade, Number(shot.Made));

                    foreach (var decision in shot.FreeThrows.SelectMany(FreeThrow))
                        yield return decision;

                    if (shot.OffensiveRebound is { } shotRebound)
                        yield return Decision(NbaNodes.OffensiveRebound, Number(shotRebound));

                    break;
                case ObservedFoul foul:
                    yield return Decision(NbaNodes.InBonus, Number(foul.InBonus));

                    foreach (var decision in foul.FreeThrows.SelectMany(FreeThrow))
                        yield return decision;

                    if (foul.OffensiveRebound is { } foulRebound)
                        yield return Decision(NbaNodes.OffensiveRebound, Number(foulRebound));

                    break;
                case ObservedTurnover:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(possession), action.GetType().Name, "Not an observed action");
            }
        }

        var last = possession.Actions[^1];
        var after = last.EndsPossession && !last.PeriodEndedBeforeRebound ? Sides.Opponent(team) : team;

        foreach (var decision in Timeouts(possession.TimeoutsAfter, after))
            yield return decision;

        foreach (var decision in Awarded(possession.AwardedAfter, after))
            yield return decision;

        IEnumerable<ObservedDecision> FreeThrow(ObservedFreeThrow freeThrow) =>
            [.. Timeouts(freeThrow.TimeoutsBefore, team), Decision(NbaNodes.FreeThrowMade, Number(freeThrow.Made))];

        IEnumerable<ObservedDecision> Timeouts(int[] callers, int inPossession) =>
            callers.Select(caller => new ObservedDecision(caller == inPossession ? NbaNodes.OffenceTimeout : NbaNodes.DefenceTimeout, _ => 1, inPossession, possession.Period));

        IEnumerable<ObservedDecision> Awarded(AwardedFreeThrow[] awarded, int inPossession) =>
            awarded.SelectMany(f => new[]
            {
                new ObservedDecision(NbaNodes.AwardedFreeThrow, _ => AwardedFreeThrows.CategoryOf(f.Kind, f.Team != inPossession) ?? throw new RulesDisagreeWithDataException($"The rules have no category for a {f.Kind} free throw to the {(f.Team != inPossession ? "defence" : "offence")}"), inPossession, possession.Period),
                new ObservedDecision(NbaNodes.AwardedFreeThrowMade, _ => Number(f.Made), inPossession, possession.Period),
            });

        ObservedDecision Decision(NodeSpec<NbaState> node, int outcome) =>
            new(node, _ => outcome, team, possession.Period);
    }

    private static int CandidateRow(ChoiceNode<NbaState> node, NbaState state, int player)
    {
        var row = Array.IndexOf(node.CandidatesIn(state), player);

        return row >= 0 ? row : throw new RulesDisagreeWithDataException($"Player {player} acts in period {state.Period} at {state.PeriodClock} seconds left, but is not among the candidates of {node.Name}");
    }

    private static int Number(bool value) => value ? 1 : 0;
}
