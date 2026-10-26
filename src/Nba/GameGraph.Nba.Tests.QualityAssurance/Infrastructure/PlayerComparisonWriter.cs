using GameGraph.Nba.Tests.QualityAssurance.Definitions;
using Parquet;
using Parquet.Schema;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class PlayerComparisonWriter
{
    public static async Task Write(string path, IReadOnlyList<GameComparison> games)
    {
        var rows = games
            .SelectMany((g, index) => g.Real.Players.Keys.Order().Select(player => (Game: index, Player: player, Real: g.Real.Players[player], Simulated: g.Simulated.Players[player])))
            .ToArray();
        DataField<int>[] fields = [new("game"), new("player"), new("real started"), new("simulated started"), new("real seconds"), new("simulated seconds"), new("real rebounds"), new("simulated rebounds"), new("real turnovers"), new("simulated turnovers"), new("real fouls"), new("simulated fouls"), new("real points"), new("simulated points"), new("real attempts"), new("simulated attempts"), new("real threes"), new("simulated threes"), new("real free throws"), new("simulated free throws"), new("real assists"), new("simulated assists"), new("real steals"), new("simulated steals"), new("real blocks"), new("simulated blocks")];
        int[][] columns =
        [
            [.. rows.Select(r => r.Game)],
            [.. rows.Select(r => r.Player)],
            [.. rows.Select(r => r.Real.Started ? 1 : 0)],
            [.. rows.Select(r => r.Simulated.Started ? 1 : 0)],
            [.. rows.Select(r => r.Real.Seconds)],
            [.. rows.Select(r => r.Simulated.Seconds)],
            [.. rows.Select(r => r.Real.Rebounds)],
            [.. rows.Select(r => r.Simulated.Rebounds)],
            [.. rows.Select(r => r.Real.Turnovers)],
            [.. rows.Select(r => r.Simulated.Turnovers)],
            [.. rows.Select(r => r.Real.Fouls)],
            [.. rows.Select(r => r.Simulated.Fouls)],
            [.. rows.Select(r => r.Real.Points)],
            [.. rows.Select(r => r.Simulated.Points)],
            [.. rows.Select(r => r.Real.FieldGoalAttempts)],
            [.. rows.Select(r => r.Simulated.FieldGoalAttempts)],
            [.. rows.Select(r => r.Real.ThreePointAttempts)],
            [.. rows.Select(r => r.Simulated.ThreePointAttempts)],
            [.. rows.Select(r => r.Real.FreeThrowAttempts)],
            [.. rows.Select(r => r.Simulated.FreeThrowAttempts)],
            [.. rows.Select(r => r.Real.Assists)],
            [.. rows.Select(r => r.Simulated.Assists)],
            [.. rows.Select(r => r.Real.Steals)],
            [.. rows.Select(r => r.Simulated.Steals)],
            [.. rows.Select(r => r.Real.Blocks)],
            [.. rows.Select(r => r.Simulated.Blocks)],
        ];

        await using var stream = File.Create(path);
        await using var writer = await ParquetWriter.CreateAsync(new ParquetSchema(fields), stream);
        using var group = writer.CreateRowGroup();

        for (var c = 0; c < fields.Length; c++)
            await group.WriteAsync<int>(fields[c], columns[c]);
    }
}
