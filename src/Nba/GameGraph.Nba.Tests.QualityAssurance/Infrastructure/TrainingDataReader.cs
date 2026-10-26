using GameGraph.Contract.Files;
using GameGraph.Contract;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using GameGraph.Nba.Tests.QualityAssurance.Domain;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class TrainingDataReader
{
    public static (TrainingDataManifest Manifest, ExampleTable Examples) Read(string node)
    {
        var data = TrainingDataDirectory.Read(DataDirectories.TrainingData(node));

        return (data.Manifest, CollectedExamples.Table(data.Manifest.Features, data.Games));
    }

    public static string[] GameIds(string node) => TrainingDataDirectory.GameIds(DataDirectories.TrainingData(node));
}
