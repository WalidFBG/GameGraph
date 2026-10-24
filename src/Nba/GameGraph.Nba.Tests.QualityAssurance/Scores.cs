using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class Scores(int simulations)
{
    public int[,] Points { get; } = new int[simulations, 2];

    public int[] Possessions { get; } = new int[simulations];

    public ScoreCollector For(int simulationIndex) => new(this, simulationIndex);

    public int Home(int simulationIndex) => Points[simulationIndex, Sides.Home];

    public int Away(int simulationIndex) => Points[simulationIndex, Sides.Away];
}
