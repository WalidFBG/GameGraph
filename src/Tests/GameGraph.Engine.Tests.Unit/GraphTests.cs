using Xunit;

namespace GameGraph.Engine.Tests.Unit;

public sealed class GraphTests
{
    [Fact]
    public void EachNodeIsIndexedInTheOrderItWasCreated()
    {
        var game = new ToyGame();

        Assert.Equal(0, game.Graph.IndexOf(game.Wide));
        Assert.Equal(1, game.Graph.IndexOf(game.Narrow));
        Assert.Equal([game.Wide, game.Narrow], game.Graph.Nodes);
    }

    [Fact]
    public void AGraphHasNoIndexForAnotherGraphsNode()
    {
        var game = new ToyGame();

        Assert.Throws<ArgumentException>(() => game.Graph.IndexOf(new ToyGame().Narrow));
        Assert.Throws<ArgumentException>(() => game.Graph.IndexOf(ToyGame.Ending(new GraphBuilder<ToyState>(), "Alone")));
    }

    [Fact]
    public void AGraphContainsOnlyItsOwnNodes()
    {
        var game = new ToyGame();
        var other = new ToyGame();

        Assert.True(game.Graph.Contains(game.Narrow));
        Assert.False(game.Graph.Contains(other.Narrow));
        Assert.False(game.Graph.Contains(ToyGame.Ending(new GraphBuilder<ToyState>(), "Alone")));
    }

    [Fact]
    public void AGraphIsBuiltOnce()
    {
        var builder = new GraphBuilder<ToyState>();
        var node = ToyGame.Ending(builder, "Node");
        _ = builder.Build(node);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build(node));

        Assert.Equal("The graph is already built", exception.Message);
    }

    [Fact]
    public void ANodeCannotJoinABuiltGraph()
    {
        var builder = new GraphBuilder<ToyState>();
        _ = builder.Build(ToyGame.Ending(builder, "Node"));

        var exception = Assert.Throws<InvalidOperationException>(() => ToyGame.Ending(builder, "Late"));

        Assert.Equal("Late joins a graph that is already built", exception.Message);
    }

    [Fact]
    public void NodeNamesMustBeUnique()
    {
        var builder = new GraphBuilder<ToyState>();
        var first = ToyGame.Ending(builder, "Same");
        _ = ToyGame.Ending(builder, "Same");

        Assert.Throws<ArgumentException>(() => builder.Build(first));
    }

    [Fact]
    public void TheStartMustBeOneOfTheGraphsNodes()
    {
        var builder = new GraphBuilder<ToyState>();
        _ = ToyGame.Ending(builder, "Node");

        Assert.Throws<ArgumentException>(() => builder.Build(ToyGame.Ending(new GraphBuilder<ToyState>(), "Start")));
    }
}
