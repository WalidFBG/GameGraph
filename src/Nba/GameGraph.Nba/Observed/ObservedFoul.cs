using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedFoul : ObservedAction
{
    public ObservedFoul(int clock, bool[] freeThrows, bool? offensiveRebound)
        : base(clock)
    {
        if (freeThrows.Length is not (0 or Graph.FreeThrows.Bonus))
            throw new ArgumentException($"A non-shooting foul gives 0 or {Graph.FreeThrows.Bonus} free throws, got {freeThrows.Length}", nameof(freeThrows));

        CheckRebound(freeThrows.Length > 0 && !freeThrows[^1], offensiveRebound);

        FreeThrows = freeThrows;
        OffensiveRebound = offensiveRebound;
    }

    public bool[] FreeThrows { get; }

    public bool? OffensiveRebound { get; }

    public bool InBonus => FreeThrows.Length > 0;

    public override ActionType Type => ActionType.NonShootingFoul;

    public override bool EndsPossession => InBonus && OffensiveRebound != true;
}
