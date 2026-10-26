using GameGraph.Contract.Distributions;
using GameGraph.Contract.Terms;
using GameGraph.Engine.Nodes;
using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class NodeTests
{
    private readonly GraphBuilder<ToyState> _graph = new();

    [Fact]
    public void ANodesFeaturesAreThoseOfItsTermsAndGivenInTheOrderTheyFirstAppear()
    {
        var node = new SingleRowNode<ToyState>(
            _graph,
            "Timed",
            new DiscreteJohnsonSuDistribution(),
            new Dictionary<string, FeatureTerm<ToyState>[]>
            {
                ["location"] = [FeatureTerm.Linear(ToyGame.Triple), FeatureTerm.Spline(ToyGame.Value, 4)],
                ["scale"] = [FeatureTerm.Linear(ToyGame.Value)],
                ["skew"] = [],
                ["tail"] = [FeatureTerm.Linear(ToyGame.Double)],
            },
            new Dictionary<string, Feature<ToyState>> { ["maximum"] = ToyGame.Negated },
            (_, _) => Next<ToyState>.GameOver);

        Assert.Equal(["Triple", "Value", "Double", "Negated"], node.Features.Select(f => f.Name));
        Assert.Equal(new LinearSplineTerm("Value", 4), node.Model.Terms["location"][1]);
        Assert.Equal("Negated", node.Model.Given["maximum"]);
    }

    [Fact]
    public void DifferentFeaturesWithTheSameNameFail()
    {
        var impostor = new Feature<ToyState>("Value", s => s.Value + 1);

        var exception = Assert.Throws<ArgumentException>(() => new SingleRowNode<ToyState>(
            _graph,
            "Confused",
            new DiscreteJohnsonSuDistribution(),
            new Dictionary<string, FeatureTerm<ToyState>[]>
            {
                ["location"] = [FeatureTerm.Linear(ToyGame.Value)],
                ["scale"] = [FeatureTerm.Linear(impostor)],
                ["skew"] = [],
                ["tail"] = [],
            },
            new Dictionary<string, Feature<ToyState>> { ["maximum"] = ToyGame.Value },
            (_, _) => Next<ToyState>.GameOver));

        Assert.StartsWith("Confused has different features named Value", exception.Message);
    }
}
