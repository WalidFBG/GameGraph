namespace GameGraph.Nba.Observed;

public abstract class ObservedGame(string id, DateTime startUtc, long homeTeamId, long awayTeamId)
{
    public string Id { get; } = id;

    public DateTime StartUtc { get; } = startUtc;

    public long HomeTeamId { get; } = homeTeamId;

    public long AwayTeamId { get; } = awayTeamId;
}
