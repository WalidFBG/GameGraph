using GameGraph.Contract.Distributions;

namespace GameGraph.Engine.Tests.Unit;

internal sealed class ToyGame
{
    public static readonly Feature<ToyState> Value = new(nameof(Value), s => s.Value);

    public static readonly Feature<ToyState> Double = new(nameof(Double), s => 2 * s.Value);

    public static readonly Feature<ToyState> Triple = new(nameof(Triple), s => 3 * s.Value);

    public static readonly Feature<ToyState> Negated = new(nameof(Negated), s => -s.Value);

    public ToyGame()
    {
        Wide = Node(nameof(Wide), [Value, Double, Triple], AfterWide);
        Narrow = Node(nameof(Narrow), [Negated], AfterNarrow);
        Spec = new([Wide, Narrow], Wide);
    }

    public NodeSpec<ToyState> Wide { get; }

    public NodeSpec<ToyState> Narrow { get; }

    public GameSpec<ToyState> Spec { get; }

    public static NodeSpec<ToyState> Node(string name, Feature<ToyState>[] features, Transition<ToyState> transition)
    {
        var distribution = new BernoulliDistribution();
        FeatureTerm<ToyState>[] terms = [.. features.Select(FeatureTerm.Linear)];

        return new VectorNode<ToyState>(name, distribution, distribution.Predicted().ToDictionary(p => p.Name, _ => terms), new Dictionary<string, Feature<ToyState>>(), transition);
    }

    private Next<ToyState> AfterWide(ToyState state, int outcome)
    {
        state.Value += 1 + outcome;
        return Narrow;
    }

    private Next<ToyState> AfterNarrow(ToyState state, int outcome) => state.Value < 2 ? Wide : Next<ToyState>.GameOver;

    public static NodeSpec<ToyState> Ending(string name) => Node(name, [Value], (_, _) => Next<ToyState>.GameOver);

    public static NodeSpec<ToyState> Leading(string name, NodeSpec<ToyState> target) => Node(name, [Value], (_, _) => target);
}
