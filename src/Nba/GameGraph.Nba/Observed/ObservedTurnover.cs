using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedTurnover : ObservedAction
{
    public ObservedTurnover(int clock, int? player, bool stolen)
        : base(clock)
    {
        if (stolen && player is null)
            throw new ArgumentException("A steal takes the ball from a player", nameof(player));

        Player = player;
        Stolen = stolen;
    }

    public int? Player { get; }

    public bool Stolen { get; }

    public bool ByTeam => Player is null;

    public override ActionType Type => ActionType.Turnover;

    public override bool EndsPossession => true;
}
