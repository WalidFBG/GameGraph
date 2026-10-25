using GameGraph.Models;
using GameGraph.Nba.Audit.Definitions;

namespace GameGraph.Nba.Audit.Domain;

internal static class ExampleScorer
{
    public static ScoredExamples Score(ExampleTable examples, Model model)
    {
        var features = examples.Features;
        var outcomes = features.Max(f => model.Outcomes(f));
        var probabilities = Enumerable.Range(0, outcomes).Select(_ => new double[features.Length]).ToArray();

        Parallel.For(0, features.Length, e =>
        {
            for (var outcome = 0; outcome < model.Outcomes(features[e]); outcome++)
                probabilities[outcome][e] = Math.Exp(model.LogProbability(features[e], outcome));
        });

        return new ScoredExamples(examples, probabilities);
    }
}
