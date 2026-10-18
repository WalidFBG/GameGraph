using System.Text;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Possessions;

public sealed class PossessionSummary
{
    private readonly Dictionary<RejectionReason, int> rejections = [];
    private readonly SortedDictionary<int, int> differences = [];
    private readonly List<int> possessionsPerTeam = [];
    private int games;
    private int scored;
    private int secondBaskets;

    public void Add(PossessionReading reading)
    {
        games++;

        if (!reading.IsAccepted)
        {
            rejections[reading.Rejection] = rejections.GetValueOrDefault(reading.Rejection) + 1;
            return;
        }

        var home = reading.Possessions.Count(p => p.Team == TeamType.HomeOrA);
        var away = reading.Possessions.Length - home;

        possessionsPerTeam.Add(home);
        possessionsPerTeam.Add(away);
        differences[home - away] = differences.GetValueOrDefault(home - away) + 1;
        scored += reading.Possessions.Count(p => p.Scored);
        secondBaskets += reading.Possessions.Count(p => p.MadeFieldGoals > 1);
    }

    public string Describe()
    {
        var accepted = possessionsPerTeam.Count / 2;
        var text = new StringBuilder();

        text.AppendLine($"Games read: {games}");
        text.AppendLine($"Games accepted: {accepted}");

        foreach (var (reason, count) in rejections.OrderBy(r => r.Key))
            text.AppendLine($"Games rejected, {reason}: {count}");

        if (accepted == 0)
            return text.ToString();

        text.AppendLine($"Possessions per team per game: min {possessionsPerTeam.Min()}, mean {possessionsPerTeam.Average():F1}, max {possessionsPerTeam.Max()}");
        text.AppendLine($"Possessions scoring a made field goal: {(double)scored / possessionsPerTeam.Sum():P1}");
        text.AppendLine($"Possessions with a second made field goal (ignored in step 1): {secondBaskets}");
        text.AppendLine("Home minus away possessions:");

        foreach (var (difference, count) in differences)
            text.AppendLine($"  {difference,3}: {count}");

        return text.ToString();
    }
}
