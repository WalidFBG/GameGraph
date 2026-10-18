using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedTurnover : ObservedAction
{
    public override ActionType Type => ActionType.Turnover;

    public override bool EndsPossession => true;
}
