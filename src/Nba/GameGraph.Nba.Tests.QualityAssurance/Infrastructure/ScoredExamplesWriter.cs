using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using Parquet;
using Parquet.Schema;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class ScoredExamplesWriter
{
    public static async Task Write(string path, ScoredExamples scored)
    {
        var examples = scored.Examples;
        var gameField = new DataField<int>("game");
        var featureFields = examples.FeatureNames.Select(f => new DataField<double>(f)).ToArray();
        var outcomeField = new DataField<int>("outcome");
        var probabilityFields = Enumerable.Range(0, scored.Probabilities.Length).Select(o => new DataField<double>($"p{o}")).ToArray();

        await using var stream = File.Create(path);
        await using var writer = await ParquetWriter.CreateAsync(new ParquetSchema([gameField, .. featureFields, outcomeField, .. probabilityFields]), stream);
        using var group = writer.CreateRowGroup();

        await group.WriteAsync<int>(gameField, examples.Games);

        for (var f = 0; f < featureFields.Length; f++)
            await group.WriteAsync<double>(featureFields[f], examples.Features.Select(e => e[f]).ToArray());

        await group.WriteAsync<int>(outcomeField, examples.Outcomes);

        for (var o = 0; o < probabilityFields.Length; o++)
            await group.WriteAsync<double>(probabilityFields[o], scored.Probabilities[o]);
    }
}
