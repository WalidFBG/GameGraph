using GameGraph.Contract.Distributions;
using GameGraph.Contract.Terms;
using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class ChoiceNodeTests
{
    private static readonly int[] Candidates = [3, 4, 7];

    private static readonly int[] Pair = [1, 2];

    private static readonly int[] NoCandidates = [];

    private static readonly CandidateFeature<ToyState> Distance = new(nameof(Distance), (s, candidate) => candidate - s.Value);

    private static readonly CandidateFeature<ToyState> Odd = new(nameof(Odd), (_, candidate) => candidate % 2);

    private static readonly Feature<ToyState> Two = new(nameof(Two), _ => 2);

    private static readonly Feature<ToyState> None = new(nameof(None), _ => 0);

    [Fact]
    public void AChoiceNodeWritesOneRowOfFeaturesPerCandidateEachWithItsSize()
    {
        var node = new ChoiceNode<ToyState>("Pick", _ => Candidates, Two, [CandidateTerm.Linear(Distance), CandidateTerm.Spline(Odd, 2)], (_, _) => Next<ToyState>.GameOver);
        var state = new ToyState { Value = 2 };
        var buffer = Array.Empty<double>();

        Assert.Equal([1, 1, 2, 2, 0, 2, 5, 1, 2], node.WriteFeatures(state, ref buffer).ToArray());
        Assert.Equal(["Distance", "Odd", "Two"], node.FeatureNames);
        Assert.IsType<ChoiceDistribution>(node.Model.Distribution);
        Assert.Equal([new LinearTerm("Distance"), new LinearSplineTerm("Odd", 2)], node.Model.Terms["utility"]);
        Assert.Equal("Two", node.Model.Given[ChoiceDistribution.Size]);
    }

    [Fact]
    public void AChoiceOfEveryCandidateHasOneOutcomeAndIsNotRecorded()
    {
        var forced = new ChoiceNode<ToyState>("Forced", _ => Pair, Two, [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver);
        var pick = new ChoiceNode<ToyState>("Pick", _ => Candidates, Two, [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver);
        var collector = new RecordingDecisionCollector<ToyState>(new GameSpec<ToyState>([forced, pick], forced));
        var state = new ToyState();
        var buffer = Array.Empty<double>();

        collector.BeginGame("game", DateTime.UnixEpoch);
        collector.Collect(forced, state, forced.WriteFeatures(state, ref buffer), 0b11);
        collector.Collect(pick, state, pick.WriteFeatures(state, ref buffer), 0b011);

        Assert.True(forced.HasOneOutcome(state));
        Assert.False(pick.HasOneOutcome(state));
        Assert.Empty(collector.GamesFor(forced)[0].Examples);
        Assert.Single(collector.GamesFor(pick)[0].Examples);
    }

    [Fact]
    public void AChoiceOfMoreThanItsCandidatesFailsLoudly()
    {
        var node = new ChoiceNode<ToyState>("Empty", _ => NoCandidates, Two, [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver);
        var buffer = Array.Empty<double>();

        Assert.Throws<InvalidOperationException>(() => node.WriteFeatures(new ToyState(), ref buffer));
    }

    [Fact]
    public void AChoiceOfNoneHasOneOutcomeAndIsNotRecorded()
    {
        var none = new ChoiceNode<ToyState>("None", _ => Candidates, None, [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver);
        var collector = new RecordingDecisionCollector<ToyState>(new GameSpec<ToyState>([none], none));
        var state = new ToyState();
        var buffer = Array.Empty<double>();

        collector.BeginGame("game", DateTime.UnixEpoch);
        collector.Collect(none, state, none.WriteFeatures(state, ref buffer), 0);

        Assert.True(none.HasOneOutcome(state));
        Assert.Empty(collector.GamesFor(none)[0].Examples);
    }

    [Fact]
    public void TheWalkPassesTheChosenRowsCandidatesToTheTransition()
    {
        int[] chosen = [];
        var pick = new ChoiceNode<ToyState>("Pick", _ => Candidates, Two, [CandidateTerm.Linear(Distance)], (_, candidates) =>
        {
            chosen = candidates.ToArray();
            return Next<ToyState>.GameOver;
        });
        var recorder = new Recorder();

        GameWalker.Walk(new GameSpec<ToyState>([pick], pick), new ToyState { Value = 1 }, recorder, recorder);

        Assert.Equal([[2, 2, 3, 2, 6, 2]], recorder.Collected.Select(c => c.Features));
        Assert.Equal([3], chosen);
    }

    [Fact]
    public void ACandidatesRowsAreTheirBits()
    {
        var pick = new ChoiceNode<ToyState>("Pick", _ => Candidates, Two, [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver);

        Assert.True(pick.TryRowsOf(new ToyState(), [7, 3], out var rows));
        Assert.Equal(0b101, rows);
    }

    [Fact]
    public void RowsOfAPlayerWhoIsNotACandidateOrIsChosenTwiceAreNotFound()
    {
        var pick = new ChoiceNode<ToyState>("Pick", _ => Candidates, Two, [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver);

        Assert.False(pick.TryRowsOf(new ToyState(), [5], out _));
        Assert.False(pick.TryRowsOf(new ToyState(), [4, 4], out _));
    }

    [Fact]
    public void AChoiceNodeWithoutFeaturesFails() =>
        Assert.Throws<ArgumentException>(() => new ChoiceNode<ToyState>("Blind", _ => Pair, Two, [], (_, _) => Next<ToyState>.GameOver));

    [Fact]
    public void AChoiceNodeWhoseSizeIsNamedLikeACandidateFeatureFails() =>
        Assert.Throws<ArgumentException>(() => new ChoiceNode<ToyState>("Pick", _ => Pair, new Feature<ToyState>(nameof(Distance), _ => 1), [CandidateTerm.Linear(Distance)], (_, _) => Next<ToyState>.GameOver));

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
