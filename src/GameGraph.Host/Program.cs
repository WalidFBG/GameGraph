using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Sportradar;
using GameGraph.Nba.TrainingDataGeneration;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Host;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length is 3 or 4 && args[0] == "generate-training-data" && (args.Length == 3 || args[3] == "--replace"))
        {
            GenerateTrainingData(args[1], args[2], replace: args.Length == 4);
            return 0;
        }

        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  GameGraph generate-training-data <validated data directory> <training data directory> [--replace]");
        return 1;
    }

    private static void GenerateTrainingData(string validatedData, string trainingData, bool replace)
    {
        var games = new ValidatedGameReader(validatedData).ReadAll();
        var tally = new GameTally();
        var collector = new TrainingDataCollector<NbaState>(NbaGameSpec.Instance);

        new TrainingDataSeason(collector, tally).Walk(games, new TeamHistory());

        Console.WriteLine(tally.Describe());

        var writer = new TrainingDataWriter(trainingData, replace);

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var examples = collector.ExamplesFor(node);
            var directory = writer.Write(node, validatedData, examples, tally.ToDictionary());

            Console.WriteLine($"{node.Name}: {examples.Count} examples, written to {directory}");
        }
    }
}
