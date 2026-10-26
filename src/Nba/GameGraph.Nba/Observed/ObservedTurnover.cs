using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedTurnover : ObservedAction
{
    public ObservedTurnover(int clock, bool stolen)
        : base(clock) =>
        Stolen = stolen;

    public bool Stolen { get; }

    public override ActionType Type => ActionType.Turnover;

    public override bool EndsPossession => true;
}
