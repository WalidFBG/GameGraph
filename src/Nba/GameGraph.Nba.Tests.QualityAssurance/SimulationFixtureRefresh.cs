using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Fixture;
using GameGraph.Nba.Tests.QualityAssurance.Infrastructure;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class SimulationFixtureRefresh(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public void WritesTheLatestGameAndTheFittedModelsAsTheSimulationFixture()
    {
        var (game, context) = GeneratedGames.InOrder(ValidatedDataReader.ReadAll()).Last();

        SimulationFixture.Write(context, DataDirectories.FittedModel);

        output.WriteLine($"The simulation fixture is game {game.Id} of {game.StartUtc:o} with the fitted models of {Path.GetDirectoryName(DataDirectories.FittedModel(string.Empty))}");
    }
}
