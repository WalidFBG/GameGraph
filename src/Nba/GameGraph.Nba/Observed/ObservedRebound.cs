namespace GameGraph.Nba.Observed;

public readonly record struct ObservedRebound(bool Offensive, int? Rebounder)
{
    public bool ByTeam => Rebounder is null;
}
