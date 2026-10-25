namespace GameGraph.Nba.Observed;

public sealed class RejectedGame : ObservedGame
{
    public RejectedGame(string id, DateTime startUtc, long homeTeamId, long awayTeamId, RejectionReason reason)
        : base(id, startUtc, homeTeamId, awayTeamId)
    {
        if (reason == RejectionReason.NotSet)
            throw new ArgumentException($"Game {id} is rejected without a reason", nameof(reason));

        Reason = reason;
    }

    public RejectionReason Reason { get; }
}
