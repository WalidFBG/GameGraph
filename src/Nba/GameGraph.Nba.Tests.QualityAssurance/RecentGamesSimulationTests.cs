using System.Globalization;
using GameGraph.Nba.Graph;
using GameGraph.Simulation;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class RecentGamesSimulationTests(ITestOutputHelper output)
{
    private const int Games = 200;
    private const int Simulations = 500;

    [Fact]
    public void SimulatesTheLatestGeneratedGames()
    {
        var games = QualityAssuranceData.GeneratedGames().TakeLast(Games).ToArray();
        var fitted = QualityAssuranceData.FittedGame();
        var rows = new List<Row>();

        foreach (var (game, context) in games)
        {
            var scores = new Scores(Simulations);

            Simulator.Run(fitted, Simulations, s => new NbaState(scores.For(s), context.HomeScoringRate, context.AwayScoringRate));

            var simulations = Enumerable.Range(0, Simulations).ToArray();

            rows.Add(new Row(
                game.Id,
                game.Points(Sides.Home),
                game.Points(Sides.Away),
                game.Possessions.Length,
                game.Periods > Periods.Regulation,
                simulations.Average(s => scores.Home(s)),
                simulations.Average(s => scores.Away(s)),
                simulations.Average(s => scores.Possessions[s]),
                (double)simulations.Count(s => scores.Home(s) > scores.Away(s)) / Simulations,
                (double)simulations.Count(s => scores.Periods[s] > Periods.Regulation) / Simulations));
        }

        var path = Write(rows);
        var realPossessions = rows.Average(r => r.Possessions);

        Assert.InRange(rows.Average(r => r.SimulatedPossessions), realPossessions * 0.98, realPossessions * 1.02);
        Assert.InRange(rows.Average(r => r.SimulatedOvertime), 0.01, 0.04);
        Assert.InRange(rows.Average(r => r.SimulatedHome + r.SimulatedAway - r.Home - r.Away), -8, 8);

        output.WriteLine($"{Games} games from {games[0].Game.StartUtc:d} to {games[^1].Game.StartUtc:d}, {Simulations} simulations each");
        output.WriteLine($"Total: real {rows.Average(r => r.Home + r.Away):F1}, simulated {rows.Average(r => r.SimulatedHome + r.SimulatedAway):F1}; mean absolute error {rows.Average(r => Math.Abs(r.Home + r.Away - r.SimulatedHome - r.SimulatedAway)):F1}");
        output.WriteLine($"Margin (home minus away): real {rows.Average(r => r.Home - r.Away):F1}, simulated {rows.Average(r => r.SimulatedHome - r.SimulatedAway):F1}; mean absolute error {rows.Average(r => Math.Abs(r.Home - r.Away - r.SimulatedHome + r.SimulatedAway)):F1}");
        output.WriteLine($"Possessions: real {rows.Average(r => r.Possessions):F1}, simulated {rows.Average(r => r.SimulatedPossessions):F1}");
        output.WriteLine($"Home wins: real {rows.Average(r => r.Home > r.Away ? 1.0 : 0.0):P1}, simulated {rows.Average(r => r.HomeWin):P1}; Brier score {rows.Average(r => Math.Pow(r.HomeWin - (r.Home > r.Away ? 1 : 0), 2)):F3} (always 50%: 0.250)");
        output.WriteLine($"Overtime: real {rows.Average(r => r.Overtime ? 1.0 : 0.0):P1}, simulated {rows.Average(r => r.SimulatedOvertime):P1}");
        output.WriteLine($"Written to {path}");
    }

    private static string Write(List<Row> rows)
    {
        var output = QualityAssuranceData.Output("recent-games-simulation");

        Directory.CreateDirectory(output);

        var path = Path.Combine(output, "games.csv");

        using var writer = new StreamWriter(path);

        writer.WriteLine("game,home,away,possessions,overtime,simulatedHome,simulatedAway,simulatedPossessions,homeWin,simulatedOvertime");

        foreach (var r in rows)
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{r.Game},{r.Home},{r.Away},{r.Possessions},{r.Overtime},{r.SimulatedHome:F3},{r.SimulatedAway:F3},{r.SimulatedPossessions:F3},{r.HomeWin:F4},{r.SimulatedOvertime:F4}"));

        return path;
    }

    private sealed record Row(
        string Game,
        int Home,
        int Away,
        int Possessions,
        bool Overtime,
        double SimulatedHome,
        double SimulatedAway,
        double SimulatedPossessions,
        double HomeWin,
        double SimulatedOvertime);
}
