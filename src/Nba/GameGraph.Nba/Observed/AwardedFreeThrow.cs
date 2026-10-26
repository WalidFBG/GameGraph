using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public readonly record struct AwardedFreeThrow
{
    public AwardedFreeThrow(int team, bool made, AwardedKind kind, int shooter, int? fouler)
    {
        if ((kind == AwardedKind.TakeFoul) != (fouler is not null))
            throw new ArgumentException("A take foul's free throw names its fouler, and no other awarded free throw does", nameof(fouler));

        Team = team;
        Made = made;
        Kind = kind;
        Shooter = shooter;
        Fouler = fouler;
    }

    public int Team { get; }

    public bool Made { get; }

    public AwardedKind Kind { get; }

    public int Shooter { get; }

    public int? Fouler { get; }

    public bool MovesTheBall => AwardedFreeThrows.MovesTheBall(Kind);
}
