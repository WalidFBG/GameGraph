using System.Globalization;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class SimulatedScoresWriter
{
    public static string Write(string gameId, IReadOnlyList<GameTotals> simulations)
    {
        var path = Path.Combine(DataDirectories.QualityAssurance("latest-game-simulation"), $"{gameId}.csv");

        using var writer = new StreamWriter(path);

        writer.WriteLine("simulationIndex,home,away");

        for (var simulationIndex = 0; simulationIndex < simulations.Count; simulationIndex++)
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{simulationIndex},{simulations[simulationIndex].HomePoints},{simulations[simulationIndex].AwayPoints}"));

        return path;
    }
}
