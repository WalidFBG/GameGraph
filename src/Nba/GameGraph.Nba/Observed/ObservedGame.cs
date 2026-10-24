namespace GameGraph.Nba.Observed;

public sealed record ObservedGame(string Id, DateTime StartUtc, long HomeTeamId, long AwayTeamId, RejectionReason Rejection, Possession[] Possessions, int PossessionsCutByPeriodEnd, int AwardedFreeThrowsAfterLastPlay)
{
    public bool IsAccepted => Rejection == RejectionReason.NotSet;
}
