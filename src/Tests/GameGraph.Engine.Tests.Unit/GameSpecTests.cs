using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class GameSpecTests
{
    [Fact]
    public void EachNodeIsIndexedByItsPositionInTheGame()
    {
        var game = new ToyGame();

        Assert.Equal(0, game.Wide.Index);
        Assert.Equal(1, game.Narrow.Index);
    }

    [Fact]
    public void ANodeInNoGameHasNoIndex()
    {
        var node = ToyGame.Ending("Alone");

        var exception = Assert.Throws<InvalidOperationException>(() => node.Index);

        Assert.Equal("Alone is not one of a game's nodes", exception.Message);
    }

    [Fact]
    public void ANodeBelongsToOneGameOnly()
    {
        var node = ToyGame.Ending("Shared");
        _ = new GameSpec<ToyState>([node], node);

        var exception = Assert.Throws<InvalidOperationException>(() => new GameSpec<ToyState>([node], node));

        Assert.Equal("Shared is already one of a game's nodes", exception.Message);
    }

    [Fact]
    public void AGameContainsOnlyItsOwnNodes()
    {
        var game = new ToyGame();
        var other = new ToyGame();

        Assert.True(game.Spec.Contains(game.Narrow));
        Assert.False(game.Spec.Contains(other.Narrow));
        Assert.False(game.Spec.Contains(ToyGame.Ending("Alone")));
    }

    [Fact]
    public void NodeNamesMustBeUnique()
    {
        var first = ToyGame.Ending("Same");
        var second = ToyGame.Ending("Same");

        Assert.Throws<ArgumentException>(() => new GameSpec<ToyState>([first, second], first));
    }

    [Fact]
    public void TheStartMustBeOneOfTheGamesNodes()
    {
        var node = ToyGame.Ending("Node");
        var start = ToyGame.Ending("Start");

        Assert.Throws<ArgumentException>(() => new GameSpec<ToyState>([node], start));
    }
}
