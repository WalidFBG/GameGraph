using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Sportradar;
using GameGraph.Nba.TrainingDataGeneration;
using GameGraph.Training;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Host;

public static class Program
{
    private static readonly Command Run = Command.Train;

    private const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";
    private const string Root = @"C:\Data\GameGraph\nba";
    private const string TrainingData = Root + @"\training-data";
    private const string Parameters = Root + @"\parameters";

    public static void Main()
    {
        switch (Run)
        {
            case Command.GenerateTrainingData:
                GenerateTrainingData();
                break;
            case Command.Train:
                Train();
                break;
            default:
                throw new InvalidOperationException($"Set {nameof(Run)} in {nameof(Program)} to the command to run");
        }
    }

    private static void GenerateTrainingData()
    {
        var games = new ValidatedGameReader(ValidatedData).ReadAll();
        var tally = new GameTally();
        var collector = new TrainingDataCollector<NbaState>(NbaGameSpec.Instance);

        new TrainingDataSeason(collector, tally).Walk(games, new TeamHistory());

        Console.WriteLine(tally.Describe());

        var writer = new TrainingDataWriter(TrainingData);

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var examples = collector.ExamplesFor(node);
            var directory = writer.Write(node, ValidatedData, examples, tally.ToDictionary());

            Console.WriteLine($"{node.Name}: {examples.Count} examples, written to {directory}");
        }
    }

    private static void Train()
    {
        var writer = new ParametersWriter(Parameters);

        foreach (var directory in Directory.GetDirectories(TrainingData).Order(StringComparer.Ordinal))
        {
            var data = TrainingDataReader.Read(directory);
            var fit = GradientDescent.Fit(data);

            Console.WriteLine($"{data.Node}: {data.Examples.Length} examples");

            for (var step = 0; step < fit.NegativeLogLikelihoods.Length; step += 20)
                Console.WriteLine($"  Step {step,3} NLL: {fit.NegativeLogLikelihoods[step]:F6}");

            Console.WriteLine($"  Final NLL: {fit.NegativeLogLikelihoods[^1]:F6}");
            Console.WriteLine($"  Intercept: {fit.Parameters[0]:F6}");

            for (var i = 0; i < data.Features.Length; i++)
                Console.WriteLine($"  {data.Features[i]}: {fit.Parameters[i + 1]:F6}");

            Console.WriteLine($"  Written to {writer.Write(data, fit, directory)}");
        }
    }
}
