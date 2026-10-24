namespace GameGraph.Nba.Observed;

public sealed record ObservedGame(string Id, DateTime StartUtc, long HomeTeamId, long AwayTeamId, RejectionReason Rejection, Possession[] Possessions, int PossessionsCutByPeriodEnd, int AwardedFreeThrowsAfterLastPlay)
{
    public bool IsAccepted => Rejection == RejectionReason.NotSet;

    public int Periods => Possessions.Max(p => p.Period) + 1;

    public int Points(int side) =>
        Possessions.Where(p => p.TeamInPossession == side).Sum(p => p.Points)
        + Possessions.SelectMany(p => p.Actions).SelectMany(a => a.AwardedBefore).Count(f => f.Team == side && f.Made);
}
