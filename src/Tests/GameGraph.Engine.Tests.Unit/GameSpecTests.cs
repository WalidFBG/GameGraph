using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class GameSpecTests
{
    [Fact]
    public void EachNodeIsIndexedByItsPositionInTheGame()
    {
        var game = new ToyGame();

        Assert.Equal(0, game.Spec.IndexOf(game.Wide));
        Assert.Equal(1, game.Spec.IndexOf(game.Narrow));
    }

    [Fact]
    public void AGameHasNoIndexForAnotherGamesNodeOrANodeInNoGame()
    {
        var game = new ToyGame();

        Assert.Throws<ArgumentException>(() => game.Spec.IndexOf(new ToyGame().Narrow));
        Assert.Throws<ArgumentException>(() => game.Spec.IndexOf(ToyGame.Ending("Alone")));
    }

    [Fact]
    public void ANodeBelongsToOneGameOnly()
    {
        var node = ToyGame.Ending("Shared");
        _ = new GameSpec<ToyState>([node], node);

        var exception = Assert.Throws<ArgumentException>(() => new GameSpec<ToyState>([node], node));

        Assert.StartsWith("Already in a game: Shared", exception.Message);
    }

    [Fact]
    public void AGameThatFailsToBuildLeavesItsOtherNodesFree()
    {
        var taken = ToyGame.Ending("Taken");
        _ = new GameSpec<ToyState>([taken], taken);
        var free = ToyGame.Ending("Free");

        Assert.Throws<ArgumentException>(() => new GameSpec<ToyState>([free, taken], free));

        Assert.Equal(0, new GameSpec<ToyState>([free], free).IndexOf(free));
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
