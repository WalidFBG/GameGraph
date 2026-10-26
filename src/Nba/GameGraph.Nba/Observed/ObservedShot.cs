using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedShot : ObservedAction
{
    public ObservedShot(int clock, int shooter, int points, int? fouler, bool made, int? assister, int? blocker, ObservedFreeThrow[] freeThrows, ObservedRebound? rebound)
        : base(clock)
    {
        if (points is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(points), points, "A shot is worth 2 or 3");

        var fouled = fouler is not null;
        var expected = !fouled ? 0 : made ? 1 : points;

        if (freeThrows.Length != expected)
            throw new ArgumentException($"A {(fouled ? "fouled" : "clean")} {(made ? "made" : "missed")} {points} has {expected} free throws, got {freeThrows.Length}", nameof(freeThrows));

        if (assister is not null && !made)
            throw new ArgumentException("Only a made shot is assisted", nameof(assister));

        if (blocker is not null && (made || fouled))
            throw new ArgumentException("Only a clean miss is blocked", nameof(blocker));

        CheckRebound(freeThrows.Length > 0 ? !freeThrows[^1].Made : !made, rebound);

        Shooter = shooter;
        Points = points;
        Fouler = fouler;
        Made = made;
        Assister = assister;
        Blocker = blocker;
        FreeThrows = freeThrows;
        Rebound = rebound;
    }

    public int Shooter { get; }

    public int Points { get; }

    public int? Fouler { get; }

    public bool Fouled => Fouler is not null;

    public bool Made { get; }

    public int? Assister { get; }

    public int? Blocker { get; }

    public ObservedFreeThrow[] FreeThrows { get; }

    public ObservedRebound? Rebound { get; }

    public override ActionType Type => Points == 2 ? ActionType.TwoPointAttempt : ActionType.ThreePointAttempt;

    public override bool EndsPossession => Rebound?.Offensive != true;
}
