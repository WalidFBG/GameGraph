namespace GameGraph.Nba.Possessions;

public sealed record PossessionReading(RejectionReason Rejection, Possession[] Possessions)
{
    public bool IsAccepted => Rejection == RejectionReason.NotSet;

    public static PossessionReading Accepted(Possession[] possessions) => new(RejectionReason.NotSet, possessions);

    public static PossessionReading Rejected(RejectionReason reason) => new(reason, []);
}
