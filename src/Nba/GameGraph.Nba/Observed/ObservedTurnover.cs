using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedTurnover : ObservedAction
{
    public ObservedTurnover(int clock)
        : base(clock)
    {
    }

    public override ActionType Type => ActionType.Turnover;

    public override bool EndsPossession => true;
}
