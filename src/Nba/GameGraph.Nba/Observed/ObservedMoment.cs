namespace GameGraph.Nba.Observed;

public sealed record ObservedMoment(int[] Timeouts, ObservedSubstitution[] Substitutions)
{
    public static ObservedMoment Quiet { get; } = new([], []);
}
