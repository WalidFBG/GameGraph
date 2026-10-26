using GameGraph.Contract.Distributions;
using GameGraph.Engine.Nodes;

namespace GameGraph.Engine.Tests.Unit;

internal sealed class ToyGame
{
    public static readonly Feature<ToyState> Value = new(nameof(Value), s => s.Value);

    public static readonly Feature<ToyState> Double = new(nameof(Double), s => 2 * s.Value);

    public static readonly Feature<ToyState> Triple = new(nameof(Triple), s => 3 * s.Value);

    public static readonly Feature<ToyState> Negated = new(nameof(Negated), s => -s.Value);

    public ToyGame()
    {
        var builder = new GraphBuilder<ToyState>();

        Wide = Node(builder, nameof(Wide), [Value, Double, Triple], AfterWide);
        Narrow = Node(builder, nameof(Narrow), [Negated], AfterNarrow);
        Graph = builder.Build(Wide);
    }

    public Node<ToyState> Wide { get; }

    public Node<ToyState> Narrow { get; }

    public Graph<ToyState> Graph { get; }

    public static Node<ToyState> Node(GraphBuilder<ToyState> graph, string name, Feature<ToyState>[] features, Transition<ToyState> transition)
    {
        var distribution = new BernoulliDistribution();
        FeatureTerm<ToyState>[] terms = [.. features.Select(FeatureTerm.Linear)];

        return new SingleRowNode<ToyState>(graph, name, distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, Feature<ToyState>>(), transition);
    }

    private Next<ToyState> AfterWide(ToyState state, int outcome)
    {
        state.Value += 1 + outcome;
        return Narrow;
    }

    private Next<ToyState> AfterNarrow(ToyState state, int outcome) => state.Value < 2 ? Wide : Next<ToyState>.GameOver;

    public static Node<ToyState> Ending(GraphBuilder<ToyState> graph, string name) => Node(graph, name, [Value], (_, _) => Next<ToyState>.GameOver);

    public static Node<ToyState> Leading(GraphBuilder<ToyState> graph, string name, Node<ToyState> target) => Node(graph, name, [Value], (_, _) => target);
}
