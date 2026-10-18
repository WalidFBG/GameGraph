namespace GameGraph.Nba.Observed;

public enum RejectionReason
{
    NotSet = 0,
    OutsideRegularSeason = 1,
    InvalidEvents = 2,
    UnknownActingTeam = 3,
    UnbalancedPossessions = 4,
}
