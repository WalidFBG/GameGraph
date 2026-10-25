using GameGraph.Nba.Audit.Domain;
using GameGraph.Nba.Audit.Infrastructure;
using GameGraph.Nba.Graph;
using Xunit;

namespace GameGraph.Nba.Audit;

public sealed class CalibrationAudit(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public async Task WritesEveryExampleWithTheModelsProbabilities()
    {
        var directory = AuditDirectories.NewRun("calibration");

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var (manifest, examples) = TrainingDataReader.Read(node.Name);
            var scored = ExampleScorer.Score(examples, CalibrationModel.For(node.Name, manifest, FittedModelReader.Read(node.Name)));
            var path = Path.Combine(directory, $"{node.Name}.parquet");

            await ScoredExamplesWriter.Write(path, scored);

            output.WriteLine($"{node.Name}: {examples.Outcomes.Length} examples, {scored.Probabilities.Length} outcomes, written to {path}");
        }

        output.WriteLine($"Run complete: {AuditDirectories.Complete(directory)}");
    }
}
