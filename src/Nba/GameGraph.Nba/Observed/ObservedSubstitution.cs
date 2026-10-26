using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedSubstitution
{
    public ObservedSubstitution(int team, int[] off, int[] on)
    {
        if (off.Length is 0 or > Lineups.Size || on.Length != off.Length)
            throw new ArgumentException($"A substitution swaps 1 to {Lineups.Size} players for as many, got {off.Length} off and {on.Length} on", nameof(on));

        if (off.Intersect(on).Any())
            throw new ArgumentException("A substitution cannot take off a player it puts on", nameof(on));

        Team = team;
        Off = off;
        On = on;
    }

    public int Team { get; }

    public int[] Off { get; }

    public int[] On { get; }
}
