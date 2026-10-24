namespace GameGraph.Engine;

public abstract record Next<TState>
{
    public static Next<TState> GameOver { get; } = new GameOver<TState>();

    public static implicit operator Next<TState>(NodeSpec<TState> node) => node.AsNext;
}

public sealed record NextNode<TState>(NodeSpec<TState> Node) : Next<TState>;

public sealed record GameOver<TState> : Next<TState>;
