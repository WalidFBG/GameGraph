using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed class AcceptedGame(
    string id,
    DateTime startUtc,
    long homeTeamId,
    long awayTeamId,
    long[] players,
    Rotation rotation,
    Possession[] possessions,
    int possessionsCutByPeriodEnd)
    : ObservedGame(id, startUtc, homeTeamId, awayTeamId)
{
    public long[] Players { get; } = players;

    public Rotation Rotation { get; } = rotation;

    public Possession[] Possessions { get; } = possessions;

    public int PossessionsCutByPeriodEnd { get; } = possessionsCutByPeriodEnd;

    public int Periods => Possessions.Max(p => p.Period) + 1;

    public int Points(int side) =>
        Possessions.Where(p => p.TeamInPossession == side).Sum(p => p.Points)
        + Possessions.SelectMany(p => p.Actions.SelectMany(a => a.AwardedBefore).Concat(p.AwardedAfter)).Count(f => f.Team == side && f.Made);
}
