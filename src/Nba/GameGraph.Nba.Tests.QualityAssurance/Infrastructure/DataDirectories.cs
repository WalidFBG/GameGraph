using System.Globalization;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class DataDirectories
{
    public const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";

    private const string Root = @"C:\Data\GameGraph\nba";
    private const string Partial = ".partial";

    public static string TrainingData(string node) => Path.Combine(Root, "training-data", node);

    public static string FittedModel(string node) => Path.Combine(Root, "fitted-models", $"{node}.json");

    public static string QualityAssurance(string test)
    {
        var directory = Path.Combine(Root, "quality-assurance", test);
        Directory.CreateDirectory(directory);

        return directory;
    }

    public static string NewRun(string audit)
    {
        var run = Path.Combine(Root, "audit", audit, DateTime.UtcNow.ToString("yyMMdd-HHmm", CultureInfo.InvariantCulture));

        if (Directory.Exists(run) || Directory.Exists(run + Partial))
            throw new IOException($"{run} already exists: one run of an audit a minute");

        Directory.CreateDirectory(run + Partial);

        return run + Partial;
    }

    public static string Complete(string partialRun)
    {
        if (!partialRun.EndsWith(Partial, StringComparison.Ordinal))
            throw new ArgumentException($"{partialRun} is not a run being written", nameof(partialRun));

        var run = partialRun[..^Partial.Length];
        Directory.Move(partialRun, run);

        return run;
    }
}
