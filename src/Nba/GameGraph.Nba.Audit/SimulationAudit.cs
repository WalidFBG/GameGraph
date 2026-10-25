using GameGraph.Nba.Audit.Domain;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Audit.Infrastructure;
using GameGraph.Nba.Graph;
using Xunit;

namespace GameGraph.Nba.Audit;

public sealed class SimulationAudit(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public async Task WritesEverySimulatedDecisionAndGame()
    {
        var directory = AuditDirectories.NewRun("simulation");
        var fitted = FittedModelReader.ReadGame();
        var games = GeneratedGames.InOrder(ValidatedDataReader.ReadAll()).ToArray();

        foreach (var node in NbaGameSpec.Instance.Nodes)
            TrainingDataOrigin.Require(node.Name, TrainingDataReader.GameIds(node.Name), games.Select(g => g.Game));

        var (decisions, comparisons) = SeasonSimulation.SimulateEachOnce(fitted, games);

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var examples = CollectedExamples.Table([.. node.Features.Select(f => f.Name)], decisions.GamesFor(node));
            var scored = ExampleScorer.Score(examples, fitted.ModelOf(node));
            var path = Path.Combine(directory, $"{node.Name}.parquet");

            await ScoredExamplesWriter.Write(path, scored);

            output.WriteLine($"{node.Name}: {examples.Outcomes.Length} simulated examples, {scored.Probabilities.Length} outcomes, written to {path}");
        }

        var gamesPath = Path.Combine(directory, "games.parquet");
        await GameComparisonWriter.Write(gamesPath, comparisons);

        output.WriteLine($"{comparisons.Length} games, each simulated once, written to {gamesPath}");
        output.WriteLine($"Run complete: {AuditDirectories.Complete(directory)}");
    }
}
