namespace GameGraph.Engine;

public abstract record Next<TState, TIncidentCollector>
{
    public static Next<TState, TIncidentCollector> GameOver { get; } = new GameOver<TState, TIncidentCollector>();

    public static implicit operator Next<TState, TIncidentCollector>(NodeSpec<TState, TIncidentCollector> node) => node.AsNext;
}

public sealed record NextNode<TState, TIncidentCollector>(NodeSpec<TState, TIncidentCollector> Node) : Next<TState, TIncidentCollector>;

public sealed record GameOver<TState, TIncidentCollector> : Next<TState, TIncidentCollector>;
