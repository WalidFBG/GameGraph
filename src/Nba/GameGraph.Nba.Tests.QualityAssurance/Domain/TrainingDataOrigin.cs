using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class TrainingDataOrigin
{
    public static void Require(string node, IEnumerable<string> trainingDataGameIds, IEnumerable<ObservedGame> games)
    {
        if (!trainingDataGameIds.SequenceEqual(games.Select(g => g.Id)))
            throw new InvalidDataException($"The training data of {node} was not generated from these games");
    }
}
