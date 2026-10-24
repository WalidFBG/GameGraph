namespace GameGraph.Nba.Observed;

public sealed record Possession
{
    public Possession(int teamInPossession, int period, ObservedAction[] actions)
    {
        if (actions.Length == 0)
            throw new ArgumentException("A possession needs at least one action", nameof(actions));

        if (actions[..^1].Any(a => a.EndsPossession))
            throw new ArgumentException("Only the last action of a possession can end it", nameof(actions));

        if (actions[..^1].Any(a => a.PeriodEndedBeforeRebound))
            throw new ArgumentException("Only the last action of a possession can be ended by the period", nameof(actions));

        TeamInPossession = teamInPossession;
        Period = period;
        Actions = actions;
    }

    public int TeamInPossession { get; }

    public int Period { get; }

    public ObservedAction[] Actions { get; }

    public bool CutByPeriodEnd => !Actions[^1].EndsPossession;

    public bool Scored => Actions.OfType<ObservedShot>().Any(s => s.Made);

    public int Points =>
        Actions.OfType<ObservedShot>().Sum(s => (s.Made ? s.Points : 0) + s.FreeThrows.Count(m => m))
        + Actions.OfType<ObservedFoul>().Sum(f => f.FreeThrows.Count(m => m));
}
