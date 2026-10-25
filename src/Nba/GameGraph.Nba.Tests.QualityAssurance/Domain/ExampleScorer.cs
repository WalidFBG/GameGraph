using GameGraph.Models;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class ExampleScorer
{
    public static ScoredExamples Score(ExampleTable examples, Model model)
    {
        var features = examples.Features;
        var rows = new double[features.Length][];

        Parallel.For(0, features.Length, e => rows[e] = model.Probabilities(features[e]));

        var outcomes = rows.Max(r => r.Length);
        var probabilities = Enumerable.Range(0, outcomes).Select(_ => new double[features.Length]).ToArray();

        for (var e = 0; e < rows.Length; e++)
        {
            for (var outcome = 0; outcome < rows[e].Length; outcome++)
                probabilities[outcome][e] = rows[e][outcome];
        }

        return new ScoredExamples(examples, probabilities);
    }
}
