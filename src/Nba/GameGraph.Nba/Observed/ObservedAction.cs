using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public abstract record ObservedAction
{
    protected ObservedAction(int clock)
    {
        if (clock < 0)
            throw new ArgumentOutOfRangeException(nameof(clock), clock, "An action's clock is the seconds left in its period");

        Clock = clock;
    }

    public int Clock { get; }

    public AwardedFreeThrow[] AwardedBefore { get; init; } = [];

    public bool PeriodEndedBeforeRebound { get; private set; }

    public abstract ActionType Type { get; }

    public abstract bool EndsPossession { get; }

    protected void CheckRebound(bool endedInMiss, bool? offensiveRebound)
    {
        if (!endedInMiss && offensiveRebound.HasValue)
            throw new ArgumentException("Only an action that ends in a miss has a rebound", nameof(offensiveRebound));

        PeriodEndedBeforeRebound = endedInMiss && !offensiveRebound.HasValue;

        if (PeriodEndedBeforeRebound && Clock > 0)
            throw new ArgumentException("A miss with time left needs its rebound", nameof(offensiveRebound));
    }
}
