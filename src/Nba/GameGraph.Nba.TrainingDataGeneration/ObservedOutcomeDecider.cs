using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class ObservedOutcomeDecider(Possession[] possessions) : IOutcomeDecider<NbaState, INbaIncidentCollector>
{
    private readonly ObservedDecision[] _decisions = possessions.SelectMany(Decisions).ToArray();
    private int _next;

    public int Decide(NodeSpec<NbaState, INbaIncidentCollector> node, NbaState state, double[] features)
    {
        if (_next == _decisions.Length)
            throw new InvalidDataException($"The rules asked for {node.Name} after the game's last observed decision");

        var decision = _decisions[_next];

        if (decision.Node != node || decision.TeamInPossession != state.TeamInPossession || decision.Period != state.Period)
            throw new InvalidDataException($"Decision {_next}: the data has {decision.Node.Name} for {Sides.Name(decision.TeamInPossession)} in period {decision.Period}, the rules ask for {node.Name} for {Sides.Name(state.TeamInPossession)} in period {state.Period}");

        _next++;
        return decision.Outcome;
    }

    public void EnsureAllUsed()
    {
        if (_next != _decisions.Length)
            throw new InvalidDataException($"The game ended after {_next} of {_decisions.Length} observed decisions");
    }

    private static IEnumerable<ObservedDecision> Decisions(Possession possession)
    {
        foreach (var action in possession.Actions)
        {
            yield return Decision(NbaNodes.ActionType, ActionTypes.Category(action.Type));

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

        ObservedDecision Decision(NodeSpec<NbaState, INbaIncidentCollector> node, int outcome) =>
            new(node, outcome, possession.TeamInPossession, possession.Period);
    }

    private static int Number(bool value) => value ? 1 : 0;
}
