namespace GameGraph.Engine;

public sealed class GameSpec<TState>
{
    public GameSpec(NodeSpec<TState>[] nodes, NodeSpec<TState> start)
    {
        if (nodes.Select(n => n.Name).Distinct().Count() != nodes.Length)
            throw new ArgumentException("Node names must be unique", nameof(nodes));

        if (!nodes.Contains(start))
            throw new ArgumentException($"The start node {start.Name} is not one of the game's nodes", nameof(start));

        var joined = nodes.Where(n => n.IsInGame).Select(n => n.Name).ToArray();

        if (joined.Length > 0)
            throw new ArgumentException($"Already in a game: {string.Join(", ", joined)}", nameof(nodes));

        for (var i = 0; i < nodes.Length; i++)
            nodes[i].JoinGame(i);

        Nodes = nodes;
        Start = start;
    }

    public NodeSpec<TState>[] Nodes { get; }

    public NodeSpec<TState> Start { get; }

    public bool Contains(NodeSpec<TState> node) => node.IsInGame && node.Index < Nodes.Length && Nodes[node.Index] == node;

    public int IndexOf(NodeSpec<TState> node) =>
        Contains(node) ? node.Index : throw new ArgumentException($"{node.Name} is not one of the game's nodes", nameof(node));
}
