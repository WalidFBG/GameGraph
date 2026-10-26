using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedFoul : ObservedAction
{
    public ObservedFoul(int clock, int fouler, int fouledPlayer, ObservedFreeThrow[] freeThrows, ObservedRebound? rebound)
        : base(clock)
    {
        if (freeThrows.Length is not (0 or Graph.FreeThrows.Bonus))
            throw new ArgumentException($"A non-shooting foul gives 0 or {Graph.FreeThrows.Bonus} free throws, got {freeThrows.Length}", nameof(freeThrows));

        CheckRebound(freeThrows.Length > 0 && !freeThrows[^1].Made, rebound);

        Fouler = fouler;
        FouledPlayer = fouledPlayer;
        FreeThrows = freeThrows;
        Rebound = rebound;
    }

    public int Fouler { get; }

    public int FouledPlayer { get; }

    public ObservedFreeThrow[] FreeThrows { get; }

    public ObservedRebound? Rebound { get; }

    public bool InBonus => FreeThrows.Length > 0;

    public override ActionType Type => ActionType.NonShootingFoul;

    public override bool EndsPossession => InBonus && Rebound?.Offensive != true;
}
