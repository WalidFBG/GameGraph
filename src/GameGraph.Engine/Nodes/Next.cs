namespace GameGraph.Engine.Nodes;

public abstract record Next<TState>
{
    public static Next<TState> GameOver { get; } = new GameOver<TState>();

    public static implicit operator Next<TState>(Node<TState> node) => node.AsNext;
}

public sealed record NextNode<TState>(Node<TState> Node) : Next<TState>;

public sealed record GameOver<TState> : Next<TState>;
