namespace GameGraph.Models.Tests;

internal static class Repository
{
    public static string Root { get; } = Find(AppContext.BaseDirectory);

    private static string Find(string directory) =>
        File.Exists(Path.Combine(directory, "GameGraph.slnx"))
            ? directory
            : Find(Directory.GetParent(directory)?.FullName ?? throw new InvalidOperationException("Not inside the GameGraph repository"));
}
