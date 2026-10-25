using GameGraph.Models;
using GameGraph.Models.Contract;
using GameGraph.Nba.Graph;
using Parquet;
using Parquet.Schema;
using Xunit;

namespace GameGraph.Nba.Audit;

public sealed class CalibrationAudit(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public async Task WritesEveryExampleWithTheModelsProbabilities()
    {
        var directory = AuditData.NewRun("calibration");

        foreach (var node in NbaGameSpec.Instance.Nodes)
        {
            var examples = NodeExamples.Read(AuditData.TrainingData(node.Name));
            var model = ModelFor(node.Name, examples.Manifest);
            var probabilities = Probabilities(model, examples.Features);
            var path = Path.Combine(directory, $"{node.Name}.parquet");

            await Write(path, examples, probabilities);

            output.WriteLine($"{node.Name}: {examples.Outcomes.Length} examples, {probabilities.Length} outcomes, written to {path}");
        }
    }

    private static Model ModelFor(string node, TrainingDataManifest manifest)
    {
        var fitted = ContractFiles.ReadFittedModel(AuditData.FittedModel(node));

        if (manifest.Node != node || fitted.Node != node)
            throw new InvalidDataException($"The training data of {node} is for {manifest.Node}, its fitted model for {fitted.Node}");

        if (!fitted.Model.Equals(manifest.Model))
            throw new InvalidDataException($"The fitted model of {node} was declared differently from its training data's model");

        return Model.FromFitted(fitted, manifest.Features);
    }

    private static double[][] Probabilities(Model model, double[][] features)
    {
        var outcomes = features.Max(f => model.Outcomes(f));
        var probabilities = Enumerable.Range(0, outcomes).Select(_ => new double[features.Length]).ToArray();

        Parallel.For(0, features.Length, e =>
        {
            for (var outcome = 0; outcome < model.Outcomes(features[e]); outcome++)
                probabilities[outcome][e] = Math.Exp(model.LogProbability(features[e], outcome));
        });

        return probabilities;
    }

    private static async Task Write(string path, NodeExamples examples, double[][] probabilities)
    {
        var featureFields = examples.Manifest.Features.Select(f => new DataField<double>(f)).ToArray();
        var outcomeField = new DataField<int>("outcome");
        var probabilityFields = Enumerable.Range(0, probabilities.Length).Select(o => new DataField<double>($"p{o}")).ToArray();

        await using var stream = File.Create(path);
        await using var writer = await ParquetWriter.CreateAsync(new ParquetSchema([.. featureFields, outcomeField, .. probabilityFields]), stream);
        using var group = writer.CreateRowGroup();

        for (var f = 0; f < featureFields.Length; f++)
            await group.WriteAsync<double>(featureFields[f], examples.Features.Select(e => e[f]).ToArray());

        await group.WriteAsync<int>(outcomeField, examples.Outcomes);

        for (var o = 0; o < probabilityFields.Length; o++)
            await group.WriteAsync<double>(probabilityFields[o], probabilities[o]);
    }
}
