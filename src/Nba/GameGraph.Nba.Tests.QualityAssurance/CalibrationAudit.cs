using GameGraph.Nba.Graph;
using GameGraph.Nba.Tests.QualityAssurance.Domain;
using GameGraph.Nba.Tests.QualityAssurance.Infrastructure;
using Xunit;

namespace GameGraph.Nba.Tests.QualityAssurance;

public sealed class CalibrationAudit(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public async Task WritesEveryExampleWithTheModelsProbabilities()
    {
        var directory = DataDirectories.NewRun("calibration");

        foreach (var node in NbaGraph.Instance.Nodes)
        {
            var (manifest, examples) = TrainingDataReader.Read(node.Name);
            var scored = ExampleScorer.Score(examples, CalibrationModel.For(node.Name, manifest, FittedModelReader.Read(node.Name)), manifest.Model.Distribution);
            var path = Path.Combine(directory, $"{node.Name}.parquet");

            await ScoredExamplesWriter.Write(path, scored);

            output.WriteLine($"{node.Name}: {examples.Outcomes.Length} examples, {scored.Probabilities.Length} outcomes, written to {path}");
        }

        output.WriteLine($"Run complete: {DataDirectories.Complete(directory)}");
    }
}
