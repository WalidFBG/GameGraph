using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class ObservedOutcomeDecider : IOutcomeDecider<NbaState>
{
    private readonly ObservedDecision[] _decisions;
    private int _next;

    public ObservedOutcomeDecider(Possession[] possessions) => _decisions = [.. possessions.SelectMany(Decisions)];

    public int ActionTimes { get; private set; }

    public int ClampedActionTimes { get; private set; }

    public int Decide(NodeSpec<NbaState> node, NbaState state, ReadOnlySpan<double> features)
    {
        if (node == NbaNodes.JumpBall)
        {
            var first = UpcomingIn(state.Period) ?? throw new RulesDisagreeWithDataException($"The rules ask for a jump ball in period {state.Period}, which has no observed decisions");

            return Number(first.TeamInPossession == Sides.Home);
        }

        if (node == NbaNodes.AwardedFreeThrow && !IsUpcoming(node, state))
            return AwardedFreeThrows.None;

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
        foreach (var action in possession.Actions)
        {
            foreach (var awarded in action.AwardedBefore)
            {
                yield return Decision(NbaNodes.AwardedFreeThrow, awarded.Team == possession.TeamInPossession ? AwardedFreeThrows.Offence : AwardedFreeThrows.Defence);
                yield return Decision(NbaNodes.AwardedFreeThrowMade, Number(awarded.Made));
            }

            yield return Decision(NbaNodes.ActionType, ActionTypes.Category(action.Type));
            yield return new ObservedDecision(NbaNodes.ActionTime, s => ActionTime(s, action.Clock), possession.TeamInPossession, possession.Period);

            switch (action)
            {
                case ObservedShot shot:
                    yield return Decision(NbaNodes.FouledOnShot, Number(shot.Fouled));
                    yield return Decision(NbaNodes.ShotMade, Number(shot.Made));

                    foreach (var made in shot.FreeThrows)
                        yield return Decision(NbaNodes.FreeThrowMade, Number(made));

                    if (shot.OffensiveRebound is { } shotRebound)
                        yield return Decision(NbaNodes.OffensiveRebound, Number(shotRebound));

                    break;
                case ObservedFoul foul:
                    yield return Decision(NbaNodes.InBonus, Number(foul.InBonus));

                    foreach (var made in foul.FreeThrows)
                        yield return Decision(NbaNodes.FreeThrowMade, Number(made));

                    if (foul.OffensiveRebound is { } foulRebound)
                        yield return Decision(NbaNodes.OffensiveRebound, Number(foulRebound));

                    break;
                case ObservedTurnover:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(possession), action.GetType().Name, "Not an observed action");
            }
        }

        ObservedDecision Decision(NodeSpec<NbaState> node, int outcome) =>
            new(node, _ => outcome, possession.TeamInPossession, possession.Period);
    }

    private static int Number(bool value) => value ? 1 : 0;
}
