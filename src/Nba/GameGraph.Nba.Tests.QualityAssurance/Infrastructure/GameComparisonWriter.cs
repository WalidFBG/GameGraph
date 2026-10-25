using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using Parquet;
using Parquet.Schema;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class GameComparisonWriter
{
    public static async Task Write(string path, IReadOnlyList<GameComparison> games)
    {
        var gameField = new DataField<int>("game");
        var idField = new DataField<string>("id");
        (DataField<int> Field, int[] Values)[] totals =
        [
            .. Columns("real", [.. games.Select(g => g.Real)]),
            .. Columns("simulated", [.. games.Select(g => g.Simulated)]),
        ];

        await using var stream = File.Create(path);
        await using var writer = await ParquetWriter.CreateAsync(new ParquetSchema([gameField, idField, .. totals.Select(c => c.Field)]), stream);
        using var group = writer.CreateRowGroup();

        await group.WriteAsync<int>(gameField, Enumerable.Range(0, games.Count).ToArray());
        await group.WriteAsync(idField, games.Select(g => g.Id).ToArray());

        foreach (var (field, values) in totals)
            await group.WriteAsync<int>(field, values);
    }

    private static (DataField<int> Field, int[] Values)[] Columns(string side, GameTotals[] totals) =>
    [
        (new($"{side} home points"), [.. totals.Select(t => t.HomePoints)]),
        (new($"{side} away points"), [.. totals.Select(t => t.AwayPoints)]),
        (new($"{side} possessions"), [.. totals.Select(t => t.Possessions)]),
        (new($"{side} periods"), [.. totals.Select(t => t.Periods)]),
    ];
}
