using GameGraph.Engine;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class Simulations
{
    public static GameTotals[] Of(FittedGame<NbaState> fitted, GameContext context, int simulations)
    {
        var scores = Enumerable.Range(0, simulations).Select(_ => new GameScore()).ToArray();

        Simulator.Run(
            fitted,
            Seeds.Draw(simulations),
            s => new NbaState(scores[s], context.HomePointsPerPossession, context.AwayPointsPerPossession),
            new EmptyDecisionCollector<NbaState>());

        return [.. scores.Select(s => s.Totals())];
    }
}
