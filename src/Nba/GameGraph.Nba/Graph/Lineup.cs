namespace GameGraph.Nba.Graph;

public sealed record Lineup
{
    public const int Size = 5;

    public Lineup(int period, int clock, int[] five)
    {
        if (five.Length != Size || five.Distinct().Count() != Size)
            throw new ArgumentException($"A lineup is {Size} different players, got [{string.Join(", ", five)}]", nameof(five));

        Period = period;
        Clock = clock;
        Five = five;
    }

    public int Period { get; }

    public int Clock { get; }

    public int[] Five { get; }

    public bool OnCourtAt(int period, int clock) => Period < period || (Period == period && (Clock > clock || Clock == Periods.SecondsIn(period)));
}
