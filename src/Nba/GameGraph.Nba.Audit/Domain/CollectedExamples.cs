using GameGraph.Nba.Audit.Definitions;
using GameGraph.TrainingDataGeneration;

namespace GameGraph.Nba.Audit.Domain;

internal static class CollectedExamples
{
    public static ExampleTable Table(string[] featureNames, IReadOnlyList<GameExamples> games)
    {
        var examples = games.SelectMany((g, index) => g.Examples.Select(e => (Game: index, Example: e))).ToArray();

        return new ExampleTable(
            featureNames,
            [.. examples.Select(e => e.Game)],
            [.. examples.Select(e => e.Example.Features)],
            [.. examples.Select(e => e.Example.Outcome)]);
    }
}
