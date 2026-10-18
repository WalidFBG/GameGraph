namespace GameGraph.Nba.Observed;

public sealed record ObservedGame(string Id, DateTime StartUtc, long HomeTeamId, long AwayTeamId, RejectionReason Rejection, Possession[] Possessions)
{
    public bool IsAccepted => Rejection == RejectionReason.NotSet;
}
