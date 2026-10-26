using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedTurnover : ObservedAction
{
    public ObservedTurnover(int clock, int? player, bool offensiveFoul, int? stealer)
        : base(clock)
    {
        if ((offensiveFoul || stealer is not null) && player is null)
            throw new ArgumentException("An offensive foul or a steal names its player", nameof(player));

        if (offensiveFoul && stealer is not null)
            throw new ArgumentException("An offensive foul is never a steal", nameof(stealer));

        Player = player;
        Kind = player is null ? TurnoverKinds.Team : offensiveFoul ? TurnoverKinds.OffensiveFoul : TurnoverKinds.Other;
        Stealer = stealer;
    }

    public int? Player { get; }

    public int Kind { get; }

    public int? Stealer { get; }

    public bool Stolen => Stealer is not null;

    public override ActionType Type => ActionType.Turnover;

    public override bool EndsPossession => true;
}
