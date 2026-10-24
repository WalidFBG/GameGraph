using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Nba.Sportradar;
using GameGraph.Nba.TrainingDataGeneration;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Host;

public static class Program
{
    private const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";
    private const string TrainingData = @"C:\Data\GameGraph\nba\training-data";

    public static void Main()
    {
        var games = new ValidatedGameReader(ValidatedData).ReadAll();
        var tally = new GameTally();
        var collector = new TrainingDataCollector<NbaState, INbaIncidentCollector>(NbaGameSpec.Instance);

        new TrainingDataSeason(collector, tally).Walk(games, new TeamHistory());

        Console.WriteLine(tally.Describe());

        var writer = new TrainingDataWriter(TrainingData);

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var nodeGames = collector.GamesFor(node);
            var directory = writer.Write(node, ValidatedData, nodeGames);

            Console.WriteLine($"{node.Name}: {nodeGames.Sum(g => g.Examples.Count)} examples in {nodeGames.Count} games, written to {directory}");
        }
    }
}
