using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedTurnover : ObservedAction
{
    public ObservedTurnover(int clock, int? player, bool offensiveFoul, bool stolen)
        : base(clock)
    {
        if ((offensiveFoul || stolen) && player is null)
            throw new ArgumentException("An offensive foul or a steal names its player", nameof(player));

        if (offensiveFoul && stolen)
            throw new ArgumentException("An offensive foul is never a steal", nameof(stolen));

        Player = player;
        Kind = player is null ? TurnoverKinds.Team : offensiveFoul ? TurnoverKinds.OffensiveFoul : TurnoverKinds.Other;
        Stolen = stolen;
    }

    public int? Player { get; }

    public int Kind { get; }

    public bool Stolen { get; }

    public override ActionType Type => ActionType.Turnover;

    public override bool EndsPossession => true;
}
