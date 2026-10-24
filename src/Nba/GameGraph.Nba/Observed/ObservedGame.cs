using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record ObservedGame(string Id, DateTime StartUtc, long HomeTeamId, long AwayTeamId, RejectionReason Rejection, Possession[] Possessions, int PossessionsCutByPeriodEnd)
{
    public bool IsAccepted => Rejection == RejectionReason.NotSet;

    public ObservedGame WithoutCutPossessions() => this with { Possessions = [.. Possessions.Where(p => !p.CutByPeriodEnd && !p.Actions[^1].PeriodEndedBeforeRebound)] };

    public PeriodSchedule[] Schedule() =>
        Possessions
            .GroupBy(p => p.Period)
            .OrderBy(g => g.Key)
            .Select(g => new PeriodSchedule(g.First().TeamInPossession, g.Count()))
            .ToArray();
}
