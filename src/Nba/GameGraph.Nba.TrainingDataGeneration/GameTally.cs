using System.Text;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class GameTally
{
    private readonly Dictionary<string, int> _games = [];

    public void Generated() => Add("Generated");

    public void ShortHistory() => Add("ShortHistory");

    public void Rejected(RejectionReason reason)
    {
        if (reason == RejectionReason.NotSet)
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "No rejection reason");

        Add($"Rejected{reason}");
    }

    public string Describe()
    {
        var text = new StringBuilder($"Games read: {_games.Values.Sum()}");

        foreach (var (decision, count) in _games.OrderBy(g => g.Key, StringComparer.Ordinal))
            text.AppendLine().Append($"  {decision}: {count}");

        return text.ToString();
    }

    private void Add(string decision) => _games[decision] = _games.GetValueOrDefault(decision) + 1;
}
