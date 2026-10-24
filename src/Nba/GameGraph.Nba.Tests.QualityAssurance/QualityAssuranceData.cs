using GameGraph.Models.Contract;
using GameGraph.Nba.Augmentation;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Sportradar;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.QualityAssurance;

internal static class QualityAssuranceData
{
    public const string ValidatedData = @"C:\Data\Basketball\ValidatedData\PlayByPlay";

    private const string Root = @"C:\Data\GameGraph\nba";

    public static string Output(string test) => Path.Combine(Root, "quality-assurance", test);

    public static FittedGame<NbaState> FittedGame() =>
        new(NbaGameSpec.Instance, [.. NbaGameSpec.Instance.Nodes.Select(n => ContractFiles.ReadFittedModel(Path.Combine(Root, "fitted-models", $"{n.Name}.json")))]);

    public static IEnumerable<(ObservedGame Game, GameContext Context)> GeneratedGames()
    {
        var history = new TeamHistory();

        foreach (var game in new ValidatedGameReader(ValidatedData).ReadAll().OrderBy(g => g.StartUtc).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (!game.IsAccepted)
                continue;

            if (history.IsComplete(game))
                yield return (game, history.ContextFor(game));

            history.Advance(game);
        }
    }
}
