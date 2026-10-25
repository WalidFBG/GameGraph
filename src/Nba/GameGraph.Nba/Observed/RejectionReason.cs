namespace GameGraph.Nba.Observed;

public enum RejectionReason
{
    NotSet = 0,
    OutsideRegularSeason = 1,
    InvalidEvents = 2,
    UnknownActingTeam = 3,
    UnbalancedPossessions = 4,
    UnknownShotType = 5,
    UnexpectedFoul = 6,
    UnmatchedFreeThrow = 7,
    InterruptedAction = 8,
    SameTeamTwice = 9,
    PossessionCutShort = 10,
    UnknownReboundType = 11,
    ClockRunsBackwards = 12,
    PeriodStartBreaksAlternation = 13,
    PeriodEndsMidAction = 14,
    RulesDisagreeWithData = 15,
    UnmatchedTimeout = 16,
}
