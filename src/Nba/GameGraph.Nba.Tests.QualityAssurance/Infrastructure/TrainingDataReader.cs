using GameGraph.Contract;
using GameGraph.Contract.Files;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Nba.Tests.QualityAssurance.Domain;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class TrainingDataReader
{
    public static (TrainingDataManifest Manifest, ExampleTable Examples) Read(string node)
    {
        var data = TrainingDataFiles.Read(DataDirectories.TrainingData(node));

        return (data.Manifest, CollectedExamples.Table(data.Manifest.Features, data.Games));
    }

    public static string[] GameIds(string node) => TrainingDataFiles.GameIds(DataDirectories.TrainingData(node));
}
