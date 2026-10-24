using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Sportradar;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class ObservedActionsTests(ITestOutputHelper output)
{
    [Fact]
    public void SummarisesTheObservedActions()
    {
        var games = new ValidatedGameReader(QualityAssuranceData.ValidatedData).ReadAll().ToArray();
        var accepted = games.Where(g => g.IsAccepted).ToArray();
        var possessions = accepted.SelectMany(g => g.Possessions).ToArray();
        var actions = possessions.SelectMany(p => p.Actions).ToArray();
        var shots = actions.OfType<ObservedShot>().ToArray();
        var fouls = actions.OfType<ObservedFoul>().ToArray();
        var freeThrows = shots.SelectMany(s => s.FreeThrows).Concat(fouls.SelectMany(f => f.FreeThrows)).ToArray();
        var rebounds = shots.Select(s => s.OffensiveRebound).Concat(fouls.Select(f => f.OffensiveRebound)).OfType<bool>().ToArray();

        Assert.NotEmpty(accepted);

        output.WriteLine($"Games read {games.Length}, accepted {accepted.Length}");

        foreach (var reason in games.Where(g => !g.IsAccepted).GroupBy(g => g.Rejection).OrderBy(g => g.Key))
            output.WriteLine($"  rejected {reason.Key}: {reason.Count()}");

        output.WriteLine($"Possessions {possessions.Length}, {(double)actions.Length / possessions.Length:F2} actions each; {accepted.Sum(g => g.PossessionsCutByPeriodEnd)} cut short by the end of a period");

        foreach (var type in actions.GroupBy(a => a.Type).OrderBy(g => g.Key))
            output.WriteLine($"  {type.Key}: {type.Count()} ({(double)type.Count() / actions.Length:P1})");

        foreach (var points in shots.GroupBy(s => s.Points).OrderBy(g => g.Key))
        {
            var clean = points.Where(s => !s.Fouled).ToArray();
            var fouled = points.Where(s => s.Fouled).ToArray();

            output.WriteLine($"{points.Key}-point shots: fouled {(double)fouled.Length / points.Count():P1}; made {Rate(clean.Count(s => s.Made), clean.Length)} clean, {Rate(fouled.Count(s => s.Made), fouled.Length)} fouled");
        }

        output.WriteLine($"Non-shooting fouls: {fouls.Length}, in the bonus {Rate(fouls.Count(f => f.InBonus), fouls.Length)}");
        output.WriteLine($"Free throws: {freeThrows.Length}, made {Rate(freeThrows.Count(m => m), freeThrows.Length)}");
        output.WriteLine($"Rebounds after a miss: {rebounds.Length}, offensive {Rate(rebounds.Count(o => o), rebounds.Length)}");
        var awarded = actions.SelectMany(a => a.AwardedBefore).ToArray();

        output.WriteLine($"Free throws outside the play: {(double)awarded.Length / accepted.Length:F2} a game, made {Rate(awarded.Count(f => f.Made), awarded.Length)}; {accepted.Sum(g => g.AwardedFreeThrowsAfterLastPlay)} after a game's last play");
        output.WriteLine($"Possessions scoring a made shot: {Rate(possessions.Count(p => p.Scored), possessions.Length)}");

        var periodStarts = new List<int>();
        var betweenActions = new List<int>();

        foreach (var period in accepted.SelectMany(g => g.Possessions.GroupBy(p => p.Period)))
        {
            var clocks = period.SelectMany(p => p.Actions).Select(a => a.Clock).ToArray();

            periodStarts.Add(Periods.SecondsIn(period.Key) - clocks[0]);
            betweenActions.AddRange(clocks.Zip(clocks[1..], (previous, next) => previous - next));
        }

        output.WriteLine($"Seconds from a period's start to its first action: {Spread(periodStarts)}");
        output.WriteLine($"Seconds between consecutive actions in a period: {Spread(betweenActions)}");
    }

    private static string Spread(List<int> seconds)
    {
        var sorted = seconds.Order().ToArray();
        var quantiles = new[] { 0.1, 0.25, 0.5, 0.75, 0.9, 0.99 }.Select(q => $"{q:P0} {sorted[(int)(q * (sorted.Length - 1))]}");

        return $"{sorted.Length} times; {string.Join(", ", quantiles)}, most {sorted[^1]}; over 24: {Rate(sorted.Count(s => s > 24), sorted.Length)}";
    }

    private static string Rate(int count, int total) => total == 0 ? "none" : $"{(double)count / total:P1}";
}
