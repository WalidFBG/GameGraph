using System.Text;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class GameTally
{
    private readonly Dictionary<string, int> _games = [];
    private int _actionTimes;
    private int _clampedActionTimes;

    public void Generated() => Add("Generated");

    public void ShortHistory() => Add("ShortHistory");

    public void ActionTimes(int total, int clamped)
    {
        _actionTimes += total;
        _clampedActionTimes += clamped;
    }

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

        text.AppendLine().Append($"Action times clamped to their bound: {_clampedActionTimes} of {_actionTimes} ({(double)_clampedActionTimes / _actionTimes:P2})");

        return text.ToString();
    }

    private void Add(string decision) => _games[decision] = _games.GetValueOrDefault(decision) + 1;
}
