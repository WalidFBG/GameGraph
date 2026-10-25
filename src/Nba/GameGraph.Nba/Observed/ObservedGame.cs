using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed class ObservedGame(
    string id,
    DateTime startUtc,
    long homeTeamId,
    long awayTeamId,
    RejectionReason rejection,
    long[] players,
    Rotation? rotation,
    Possession[] possessions,
    int possessionsCutByPeriodEnd)
{
    public string Id { get; } = id;

    public DateTime StartUtc { get; } = startUtc;

    public long HomeTeamId { get; } = homeTeamId;

    public long AwayTeamId { get; } = awayTeamId;

    public RejectionReason Rejection { get; } = rejection;

    public long[] Players { get; } = players;

    public Rotation Rotation => rotation ?? throw new InvalidOperationException($"Game {Id} was rejected ({Rejection}) and has no rotation");

    public Possession[] Possessions { get; } = possessions;

    public int PossessionsCutByPeriodEnd { get; } = possessionsCutByPeriodEnd;

    public bool IsAccepted => Rejection == RejectionReason.NotSet;

    public int Periods => Possessions.Max(p => p.Period) + 1;

    public int Points(int side) =>
        Possessions.Where(p => p.TeamInPossession == side).Sum(p => p.Points)
        + Possessions.SelectMany(p => p.Actions.SelectMany(a => a.AwardedBefore).Concat(p.AwardedAfter)).Count(f => f.Team == side && f.Made);
}
