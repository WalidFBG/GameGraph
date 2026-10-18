using System.Text.Json;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Data;

public sealed class ValidatedGameReader(string directory)
{
    public IEnumerable<Game> ReadAll()
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Validated data not found: {directory}");

        var paths = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal);

        foreach (var path in paths)
            yield return Read(path);
    }

    private static Game Read(string path)
    {
        using var stream = File.OpenRead(path);

        try
        {
            return JsonSerializer.Deserialize<Game>(stream, ValidatedDataJson.Options)
                ?? throw new InvalidDataException($"Empty game file: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Cannot read {path}: {exception.Message}", exception);
        }
    }
}
