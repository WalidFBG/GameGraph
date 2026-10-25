using GameGraph.Engine;
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

    public static IEnumerable<(ObservedGame Game, GameContext Context)> GeneratedGames() =>
        Augmentation.GeneratedGames.InOrder(new ValidatedGameReader(ValidatedData).ReadAll());

    public static Scores Walked(ObservedGame game, GameContext context)
    {
        var scores = new Scores(1);

        ObservedWalk.Walk(game, new NbaState(scores.For(0), context.HomeScoringRate, context.AwayScoringRate), new EmptyDecisionCollector<NbaState>());

        if (scores.Home(0) != game.Points(Sides.Home) || scores.Away(0) != game.Points(Sides.Away))
            throw new InvalidDataException($"Game {game.Id}: walking it scores {scores.Home(0)}-{scores.Away(0)}, the data {game.Points(Sides.Home)}-{game.Points(Sides.Away)}");

        return scores;
    }
}
