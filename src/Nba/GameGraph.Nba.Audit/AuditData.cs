using System.Globalization;

namespace GameGraph.Nba.Audit;

internal static class AuditData
{
    private const string Root = @"C:\Data\GameGraph\nba";

    public static string TrainingData(string node) => Path.Combine(Root, "training-data", node);

    public static string FittedModel(string node) => Path.Combine(Root, "fitted-models", $"{node}.json");

    public static string NewRun(string audit)
    {
        var directory = Path.Combine(Root, "audit", audit, DateTime.UtcNow.ToString("yyMMdd-HHmm", CultureInfo.InvariantCulture));

        if (Directory.Exists(directory))
            throw new IOException($"{directory} already exists: one run of an audit a minute");

        Directory.CreateDirectory(directory);

        return directory;
    }
}
