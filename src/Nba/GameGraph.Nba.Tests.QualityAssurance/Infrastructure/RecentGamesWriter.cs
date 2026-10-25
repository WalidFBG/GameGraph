using System.Globalization;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class RecentGamesWriter
{
    public static string Write(IReadOnlyList<RecentGameResult> results)
    {
        var path = Path.Combine(DataDirectories.QualityAssurance("recent-games-simulation"), "games.csv");

        using var writer = new StreamWriter(path);

        writer.WriteLine("game,home,away,possessions,overtime,simulatedHome,simulatedAway,simulatedPossessions,homeWin,simulatedOvertime");

        foreach (var r in results)
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{r.Game},{r.Home},{r.Away},{r.Possessions},{r.Overtime},{r.SimulatedHome:F3},{r.SimulatedAway:F3},{r.SimulatedPossessions:F3},{r.HomeWin:F4},{r.SimulatedOvertime:F4}"));

        return path;
    }
}
