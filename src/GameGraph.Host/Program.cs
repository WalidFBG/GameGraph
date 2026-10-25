using GameGraph.Contract;
using GameGraph.Contract.Files;
using GameGraph.Engine;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Sportradar;
using GameGraph.Nba.TrainingDataGeneration;

namespace GameGraph.Host;

public static class Program
{
    private const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";
    private const string TrainingDataRoot = @"C:\Data\GameGraph\nba\training-data";

    public static void Main()
    {
        var games = new ValidatedGameReader(ValidatedData).ReadAll().ToArray();
        var tally = new GameTally();
        var collector = new TrainingDataCollector<NbaState>(NbaGameSpec.Instance);

        new TrainingDataSeason(collector, tally).Walk(games);

        Console.WriteLine(tally.Describe());

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var nodeGames = collector.GamesFor(node);
            var manifest = new TrainingDataManifest(node.Name, ValidatedData, DateTime.UtcNow, node.FeatureNames, nodeGames.Sum(g => g.Examples.Length), node.Model);
            var directory = Path.Combine(TrainingDataRoot, node.Name);

            TrainingDataFiles.Write(directory, new TrainingData(manifest, nodeGames));

            Console.WriteLine($"{node.Name}: {manifest.Examples} examples in {nodeGames.Length} games, written to {directory}");
        }
    }
}
