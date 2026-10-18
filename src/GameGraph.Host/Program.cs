using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
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
        var collector = new TrainingDataCollector<NbaState, INbaIncidentCollector>(NbaGameSpec.Instance);

        new TrainingDataSeason(collector, tally).Walk(games, new TeamHistory());

        Console.WriteLine(tally.Describe());

        var writer = new TrainingDataWriter(TrainingData);

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var nodeGames = collector.GamesFor(node);
            var directory = writer.Write(node, ValidatedData, nodeGames, tally.ToDictionary());

            Console.WriteLine($"{node.Name}: {nodeGames.Sum(g => g.Examples.Count)} examples in {nodeGames.Count} games, written to {directory}");
        }
    }

    private static void Train()
    {
        var trainer = new NodeTrainer(new ParametersWriter(Parameters));

        foreach (var directory in Directory.GetDirectories(TrainingData).Order(StringComparer.Ordinal))
        {
            var (training, validation) = GameSplit.ByStartTime(TrainingDataReader.Read(directory));
            var (fit, predictors, path) = trainer.Train(training, validation, directory);

            Console.WriteLine($"{training.Node}: training {training.Examples.Length} examples in {training.Games.Length} games, validation {validation.Examples.Length} in {validation.Games.Length}");

            for (var step = 0; step <= fit.LastStep; step += 5)
                Console.WriteLine($"  Step {step,3} NLL: training {fit.TrainingNegativeLogLikelihoods[step]:F7}, validation {fit.ValidationNegativeLogLikelihoods[step]:F7}");

            Console.WriteLine($"  Best step {fit.BestStep} (stopped at {fit.LastStep}) NLL: training {fit.TrainingNegativeLogLikelihoods[fit.BestStep]:F7}, validation {fit.ValidationNegativeLogLikelihoods[fit.BestStep]:F7}");

            Console.WriteLine($"  Model: {training.Model.Family.Name}");

            foreach (var (predictor, p) in predictors.Select((predictor, p) => (predictor, p)))
            {
                Console.WriteLine($"  Predictor {p}: intercept {predictor.Intercept:F6}");

                foreach (var term in predictor.Terms)
                    Console.WriteLine($"    {term.Feature} ({term.Basis}): {string.Join(", ", term.Coefficients.Select(c => c.ToString("F6")))}");
            }

            Console.WriteLine($"  Written to {path}");
        }
    }
}
