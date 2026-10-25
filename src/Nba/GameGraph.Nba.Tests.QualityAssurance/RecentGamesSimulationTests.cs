using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Nba.Tests.QualityAssurance.Domain;
using GameGraph.Nba.Tests.QualityAssurance.Infrastructure;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class RecentGamesSimulationTests(ITestOutputHelper output)
{
    private const int Games = 200;
    private const int SimulationCount = 500;

    [Fact]
    public void SimulatesTheLatestGeneratedGames()
    {
        var games = GeneratedGames.InOrder(ValidatedDataReader.ReadAll()).TakeLast(Games).ToArray();
        var fitted = FittedModelReader.ReadGame();
        var rows = new List<RecentGameResult>();

        foreach (var (game, context) in games)
        {
            var simulations = Simulations.Of(fitted, game, context, SimulationCount);
            var real = ObservedTotals.Of(game, context);

            rows.Add(new RecentGameResult(
                game.Id,
                real.HomePoints,
                real.AwayPoints,
                real.Possessions,
                real.Periods > Periods.Regulation,
                simulations.Average(s => s.HomePoints),
                simulations.Average(s => s.AwayPoints),
                simulations.Average(s => s.Possessions),
                (double)simulations.Count(s => s.HomePoints > s.AwayPoints) / SimulationCount,
                (double)simulations.Count(s => s.Periods > Periods.Regulation) / SimulationCount));
        }

        var path = RecentGamesWriter.Write(rows);
        var realPossessions = rows.Average(r => r.Possessions);

        Assert.InRange(rows.Average(r => r.SimulatedPossessions), realPossessions * 0.98, realPossessions * 1.02);
        Assert.InRange(rows.Average(r => r.SimulatedOvertime), 0.01, 0.04);
        Assert.InRange(rows.Average(r => r.SimulatedHome + r.SimulatedAway - r.Home - r.Away), -8, 8);

        output.WriteLine($"{Games} games from {games[0].Game.StartUtc:d} to {games[^1].Game.StartUtc:d}, {SimulationCount} simulations each");
        output.WriteLine($"Total: real {rows.Average(r => r.Home + r.Away):F1}, simulated {rows.Average(r => r.SimulatedHome + r.SimulatedAway):F1}; mean absolute error {rows.Average(r => Math.Abs(r.Home + r.Away - r.SimulatedHome - r.SimulatedAway)):F1}");
        output.WriteLine($"Margin (home minus away): real {rows.Average(r => r.Home - r.Away):F1}, simulated {rows.Average(r => r.SimulatedHome - r.SimulatedAway):F1}; mean absolute error {rows.Average(r => Math.Abs(r.Home - r.Away - r.SimulatedHome + r.SimulatedAway)):F1}");
        output.WriteLine($"Possessions: real {rows.Average(r => r.Possessions):F1}, simulated {rows.Average(r => r.SimulatedPossessions):F1}");
        output.WriteLine($"Home wins: real {rows.Average(r => r.Home > r.Away ? 1.0 : 0.0):P1}, simulated {rows.Average(r => r.HomeWin):P1}; Brier score {rows.Average(r => Math.Pow(r.HomeWin - (r.Home > r.Away ? 1 : 0), 2)):F3} (always 50%: 0.250)");
        output.WriteLine($"Overtime: real {rows.Average(r => r.Overtime ? 1.0 : 0.0):P1}, simulated {rows.Average(r => r.SimulatedOvertime):P1}");
        output.WriteLine($"Written to {path}");
    }
}
