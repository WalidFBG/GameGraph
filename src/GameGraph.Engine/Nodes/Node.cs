using GameGraph.Contract;

namespace GameGraph.Engine.Nodes;

public abstract class Node<TState>
{
    private protected Node(GraphBuilder<TState> graph, string name, ModelDeclaration model, string[] featureNames)
    {
        Name = name;
        Model = model;
        FeatureNames = featureNames;
        AsNext = new(this);
        Builder = graph;
        Index = graph.Add(this);
    }

    public string Name { get; }

    public ModelDeclaration Model { get; }

    public string[] FeatureNames { get; }

    internal NextNode<TState> AsNext { get; }

    internal GraphBuilder<TState> Builder { get; }

    internal int Index { get; }

    public abstract ReadOnlySpan<double> WriteFeatures(TState state, ref double[] buffer);

    public virtual bool HasOneOutcome(TState state) => false;

    internal abstract Next<TState> Apply(TState state, int outcome);

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
