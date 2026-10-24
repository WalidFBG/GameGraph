using GameGraph.Nba.Observed;
using GameGraph.Nba.Sportradar;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class ObservedActionsTests(ITestOutputHelper output)
{
    private const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";

    [Fact]
    public void SummarisesTheObservedActions()
    {
        var games = new ValidatedGameReader(ValidatedData).ReadAll().ToArray();
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

        output.WriteLine($"Possessions {possessions.Length}, {(double)actions.Length / possessions.Length:F2} actions each; {accepted.Sum(g => g.PossessionsCutByPeriodEnd)} dropped because the period ended during them");

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
        output.WriteLine($"Possessions scoring a made shot: {Rate(possessions.Count(p => p.Scored), possessions.Length)}");
    }

    private static string Rate(int count, int total) => total == 0 ? "none" : $"{(double)count / total:P1}";
}
