using GameGraph.Models.Contract;

namespace GameGraph.Engine;

public abstract class NodeSpec<TState>
{
    private int? _index;

    private protected NodeSpec(string name, ModelDeclaration model, string[] featureNames, Transition<TState> transition)
    {
        Name = name;
        Model = model;
        FeatureNames = featureNames;
        Transition = transition;
        AsNext = new(this);
    }

    public string Name { get; }

    public ModelDeclaration Model { get; }

    public string[] FeatureNames { get; }

    internal Transition<TState> Transition { get; }

    internal NextNode<TState> AsNext { get; }

    internal bool IsInGame => _index is not null;

    internal int Index => _index ?? throw new InvalidOperationException($"{Name} is not one of a game's nodes");

    public abstract ReadOnlySpan<double> WriteFeatures(TState state, ref double[] buffer);

    internal virtual Next<TState> Apply(TState state, int outcome) => Transition(state, outcome);

    internal void JoinGame(int index) => _index = index;

    private protected static Span<double> Sized(ref double[] buffer, int count)
    {
        if (count > buffer.Length)
            buffer = new double[count];

        return buffer.AsSpan(0, count);
    }

    private protected static T[] Distinct<T>(string name, IEnumerable<T> features, Func<T, string> nameOf)
    {
        T[] distinct = [.. features.Distinct()];
        var shared = distinct.GroupBy(nameOf).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

        if (shared.Length > 0)
            throw new ArgumentException($"{name} has different features named {string.Join(", ", shared)}", nameof(features));

        return distinct;
    }
}
