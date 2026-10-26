using GameGraph.Contract.Files;
using GameGraph.Contract;
using GameGraph.Engine.Walk;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Sportradar;

namespace GameGraph.Nba.TrainingDataGeneration;

public static class Program
{
    private const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";
    private const string TrainingDataRoot = @"C:\Data\GameGraph\nba\training-data";

    public static void Main()
    {
        var games = new ValidatedGameReader(ValidatedData).ReadAll().ToArray();
        var tally = new GameTally();
        var recorder = new ExampleRecorder<NbaState>(NbaGraph.Instance);

        new TrainingDataSeason(recorder, tally).Walk(games);

        Console.WriteLine(tally.Describe());

        foreach (var node in NbaGraph.Instance.Nodes)
        {
            var nodeGames = recorder.GamesFor(node);
            var manifest = new TrainingDataManifest(node.Name, ValidatedData, DateTime.UtcNow, node.FeatureNames, nodeGames.Sum(g => g.Examples.Length), node.Model);
            var directory = Path.Combine(TrainingDataRoot, node.Name);

            TrainingDataDirectory.Write(directory, new TrainingData(manifest, nodeGames));

            Console.WriteLine($"{node.Name}: {manifest.Examples} examples in {nodeGames.Length} games, written to {directory}");
        }
    }
}
