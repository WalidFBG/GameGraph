using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Sportradar;

internal sealed class RejectedGameException(RejectionReason reason) : Exception($"Game rejected: {reason}")
{
    public RejectionReason Reason { get; } = reason;
}
