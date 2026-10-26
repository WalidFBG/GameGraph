using GameGraph.Engine.Walk;
using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class GameWalkerTests
{
    [Fact]
    public void TheWalkFollowsTheTransitionsUntilTheGameIsOver()
    {
        var game = new ToyGame();
        var recorder = new Recorder();

        GameWalker.Walk(game.Graph, new ToyState(), recorder, recorder);

        Assert.Equal(["Wide", "Narrow", "Wide", "Narrow"], recorder.Collected.Select(c => c.Node));
        Assert.Equal([0, 1, 1, 1], recorder.Collected.Select(c => c.Outcome));
    }

    [Fact]
    public void EachNodeSeesExactlyItsOwnFeatures()
    {
        var game = new ToyGame();
        var recorder = new Recorder();

        GameWalker.Walk(game.Graph, new ToyState(), recorder, recorder);

        double[][] expected = [[0, 0, 0], [-1], [1, 2, 3], [-3]];
        Assert.Equal(expected, recorder.Decided);
        Assert.Equal(expected, recorder.Collected.Select(c => c.Features));
    }

    [Fact]
    public void ATransitionToAnotherGraphsNodeFails()
    {
        var others = new GraphBuilder<ToyState>();
        var other = ToyGame.Ending(others, "Other");
        _ = others.Build(other);
        var builder = new GraphBuilder<ToyState>();
        var start = ToyGame.Leading(builder, "Start", other);
        var recorder = new Recorder();

        var exception = Assert.Throws<InvalidOperationException>(() => GameWalker.Walk(builder.Build(start), new ToyState(), recorder, recorder));

        Assert.Equal("The transition from Start returned Other, which is not one of the graph's nodes", exception.Message);
    }

    [Fact]
    public void ATransitionToANodeOfAnUnbuiltGraphFails()
    {
        var stray = ToyGame.Ending(new GraphBuilder<ToyState>(), "Stray");
        var builder = new GraphBuilder<ToyState>();
        var start = ToyGame.Leading(builder, "Start", stray);
        var recorder = new Recorder();

        var exception = Assert.Throws<InvalidOperationException>(() => GameWalker.Walk(builder.Build(start), new ToyState(), recorder, recorder));

        Assert.Equal("The transition from Start returned Stray, which is not one of the graph's nodes", exception.Message);
    }
}
