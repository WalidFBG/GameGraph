using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class GameWalkerTests
{
    [Fact]
    public void TheWalkFollowsTheTransitionsUntilTheGameIsOver()
    {
        var game = new ToyGame();
        var recorder = new Recorder();

        GameWalker.Walk(game.Spec, new ToyState(), recorder, recorder);

        Assert.Equal(["Wide", "Narrow", "Wide", "Narrow"], recorder.Collected.Select(c => c.Node));
        Assert.Equal([0, 1, 1, 1], recorder.Collected.Select(c => c.Outcome));
    }

    [Fact]
    public void EachNodeSeesExactlyItsOwnFeatures()
    {
        var game = new ToyGame();
        var recorder = new Recorder();

        GameWalker.Walk(game.Spec, new ToyState(), recorder, recorder);

        double[][] expected = [[0, 0, 0], [-1], [1, 2, 3], [-3]];
        Assert.Equal(expected, recorder.Decided);
        Assert.Equal(expected, recorder.Collected.Select(c => c.Features));
    }

    [Fact]
    public void ATransitionToAnotherGamesNodeFails()
    {
        var other = ToyGame.Ending("Other");
        _ = new GameSpec<ToyState>([other], other);
        var start = ToyGame.Leading("Start", other);
        var recorder = new Recorder();

        var exception = Assert.Throws<InvalidOperationException>(() => GameWalker.Walk(new GameSpec<ToyState>([start], start), new ToyState(), recorder, recorder));

        Assert.Equal("The transition from Start returned Other, which is not one of the game's nodes", exception.Message);
    }

    [Fact]
    public void ATransitionToANodeInNoGameFails()
    {
        var stray = ToyGame.Ending("Stray");
        var start = ToyGame.Leading("Start", stray);
        var recorder = new Recorder();

        var exception = Assert.Throws<InvalidOperationException>(() => GameWalker.Walk(new GameSpec<ToyState>([start], start), new ToyState(), recorder, recorder));

        Assert.Equal("The transition from Start returned Stray, which is not one of the game's nodes", exception.Message);
    }
}
