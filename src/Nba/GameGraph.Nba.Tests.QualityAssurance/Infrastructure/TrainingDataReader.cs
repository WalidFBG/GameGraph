using GameGraph.Models.Contract;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class TrainingDataReader
{
    public static (TrainingDataManifest Manifest, ExampleTable Examples) Read(string node)
    {
        var data = TrainingDataFiles.Read(DataDirectories.TrainingData(node));
        var gameOfExample = data.GameExamples.SelectMany((count, game) => Enumerable.Repeat(game, count)).ToArray();

        return (data.Manifest, new ExampleTable(data.Manifest.Features, gameOfExample, data.Features, data.Outcomes));
    }

    public static string[] GameIds(string node) => TrainingDataFiles.GameIds(DataDirectories.TrainingData(node));
}
