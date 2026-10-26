using System.Text.Json;
using GameGraph.Contract.Files;
using GameGraph.Nba.Graph;
using GameGraph.Simulation;

namespace GameGraph.Nba.Fixture;

public static class SimulationFixture
{
    private static readonly string Root = Path.Combine(RepositoryRoot(AppContext.BaseDirectory), "testdata", "nba-simulation");
    private static readonly string ContextPath = Path.Combine(Root, "context.json");
    private static readonly string FittedModels = Path.Combine(Root, "fitted-models");

    public static FittedGame<NbaState> Fitted() =>
        new(NbaGameSpec.Instance, [.. NbaGameSpec.Instance.Nodes.Select(n => ContractFiles.ReadFittedModel(FittedModelPath(n.Name)))]);

    public static GameContext Context() =>
        JsonSerializer.Deserialize<GameContext>(File.ReadAllText(ContextPath), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException($"{ContextPath} has no game context");

    public static void Write(GameContext context, Func<string, string> fittedModelOf)
    {
        Directory.CreateDirectory(FittedModels);

        foreach (var node in NbaGameSpec.Instance.Nodes)
            File.Copy(fittedModelOf(node.Name), FittedModelPath(node.Name), overwrite: true);

        File.WriteAllText(ContextPath, JsonSerializer.Serialize(context, JsonSerializerOptions.Web) + "\n");
    }

    private static string FittedModelPath(string node) => Path.Combine(FittedModels, $"{node}.json");

    private static string RepositoryRoot(string directory) =>
        File.Exists(Path.Combine(directory, "GameGraph.slnx"))
            ? directory
            : RepositoryRoot(Directory.GetParent(directory)?.FullName ?? throw new InvalidOperationException("Not inside the GameGraph repository"));
}
