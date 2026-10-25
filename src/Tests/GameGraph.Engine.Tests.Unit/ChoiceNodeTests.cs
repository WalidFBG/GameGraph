using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class ChoiceNodeTests
{
    private static readonly int[] Candidates = [3, 4, 7];

    private static readonly int[] Pair = [1, 2];

    private static readonly CandidateFeature<ToyState> Distance = new(nameof(Distance), (s, candidate) => candidate - s.Value);

    private static readonly CandidateFeature<ToyState> Odd = new(nameof(Odd), (_, candidate) => candidate % 2);

    [Fact]
    public void AChoiceNodeWritesOneRowOfFeaturesPerCandidate()
    {
        var node = new ChoiceNode<ToyState>("Pick", _ => Candidates, [CandidateTerm.Linear(Distance), CandidateTerm.Spline(Odd, 2)], (_, _) => Next<ToyState>.GameOver);
        var state = new ToyState { Value = 2 };
        var buffer = Array.Empty<double>();

        Assert.Equal([1, 1, 2, 0, 5, 1], node.WriteFeatures(state, ref buffer).ToArray());
        Assert.Equal(["Distance", "Odd"], node.FeatureNames);
        Assert.IsType<ChoiceDistribution>(node.Model.Distribution);
        Assert.Equal([new LinearTerm("Distance"), new LinearSplineTerm("Odd", 2)], node.Model.Terms["utility"]);
    }

    [Fact]
    public void TheWalkPassesTheChosenRowsCandidateToTheTransition()
    {
        var chosen = -1;
        var pick = new ChoiceNode<ToyState>("Pick", _ => Candidates, [CandidateTerm.Linear(Distance)], (_, candidate) =>
        {
            chosen = candidate;
            return Next<ToyState>.GameOver;
        });
        var recorder = new Recorder();

        GameWalker.Walk(new GameSpec<ToyState>([pick], pick), new ToyState { Value = 1 }, recorder, recorder);

        Assert.Equal([[2, 3, 6]], recorder.Collected.Select(c => c.Features));
        Assert.Equal(4, chosen);
    }

    [Fact]
    public void AChoiceNodeWithoutFeaturesFails() =>
        Assert.Throws<ArgumentException>(() => new ChoiceNode<ToyState>("Blind", _ => Pair, [], (_, _) => Next<ToyState>.GameOver));

    [Fact]
    public void AVectorNodeCannotChooseAmongCandidates()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VectorNode<ToyState>(
            "Pick",
            new ChoiceDistribution(),
            new Dictionary<string, FeatureTerm<ToyState>[]> { ["utility"] = [FeatureTerm.Linear(ToyGame.Value)] },
            new Dictionary<string, Feature<ToyState>>(),
            (_, _) => Next<ToyState>.GameOver));

        Assert.StartsWith("Pick chooses among candidates, so it is a choice node", exception.Message);
    }
}
