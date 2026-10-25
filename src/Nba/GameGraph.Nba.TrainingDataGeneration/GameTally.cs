using System.Text;
using GameGraph.Nba.Observed;

namespace GameGraph.Nba.TrainingDataGeneration;

public sealed class GameTally
{
    private readonly Dictionary<string, int> _games = [];
    private int _actionTimes;
    private int _clampedActionTimes;

    public void Generated() => Add("Generated", 1);

    public void ShortHistories(int count) => Add("ShortHistory", count);

    public void ActionTimes(int total, int clamped)
    {
        _actionTimes += total;
        _clampedActionTimes += clamped;
    }

    public void Rejected(RejectionReason reason) => Add($"Rejected{reason}", 1);

    public string Describe()
    {
        var text = new StringBuilder($"Games read: {_games.Values.Sum()}");

        foreach (var (decision, count) in _games.OrderBy(g => g.Key, StringComparer.Ordinal))
            text.AppendLine().Append($"  {decision}: {count}");

        text.AppendLine().Append($"Action times clamped to their bound: {_clampedActionTimes} of {_actionTimes} ({(double)_clampedActionTimes / _actionTimes:P2})");

        return text.ToString();
    }

    private void Add(string decision, int count) => _games[decision] = _games.GetValueOrDefault(decision) + count;
}
