using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public abstract record ObservedAction
{
    public abstract ActionType Type { get; }

    public abstract bool EndsPossession { get; }

    protected static void CheckRebound(bool endedInMiss, bool? offensiveRebound)
    {
        if (endedInMiss != offensiveRebound.HasValue)
            throw new ArgumentException(endedInMiss ? "An action that ends in a miss needs its rebound" : "Only an action that ends in a miss has a rebound", nameof(offensiveRebound));
    }
}
