using GameGraph.Models;
using GameGraph.Models.Contract;
using GameGraph.Nba.Tests.QualityAssurance.Definitions;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class ExampleScorer
{
    public static ScoredExamples Score(ExampleTable examples, Model model, DistributionDeclaration distribution) =>
        distribution.ChoosesAmongCandidates() ? ScoreCandidates(examples, model) : ScoreDecisions(examples, model);

    private static ScoredExamples ScoreDecisions(ExampleTable examples, Model model)
    {
        var rows = Probabilities(examples, model);
        var outcomes = rows.Max(r => r.Length);
        var probabilities = Enumerable.Range(0, outcomes).Select(_ => new double[rows.Length]).ToArray();

        for (var e = 0; e < rows.Length; e++)
        {
            for (var outcome = 0; outcome < rows[e].Length; outcome++)
                probabilities[outcome][e] = rows[e][outcome];
        }

        return new ScoredExamples(examples, probabilities);
    }

    private static ScoredExamples ScoreCandidates(ExampleTable examples, Model model)
    {
        var decisions = Probabilities(examples, model);
        var width = examples.FeatureNames.Length;
        var candidates = decisions.SelectMany((chosen, e) => chosen.Select((p, c) => (Decision: e, Candidate: c, Chosen: p))).ToArray();
        var table = new ExampleTable(
            examples.FeatureNames,
            [.. candidates.Select(c => examples.Games[c.Decision])],
            [.. candidates.Select(c => examples.Features[c.Decision][(c.Candidate * width)..((c.Candidate + 1) * width)])],
            [.. candidates.Select(c => examples.Outcomes[c.Decision] == c.Candidate ? 1 : 0)]);

        return new ScoredExamples(table, [[.. candidates.Select(c => 1 - c.Chosen)], [.. candidates.Select(c => c.Chosen)]]);
    }

    private static double[][] Probabilities(ExampleTable examples, Model model)
    {
        var rows = new double[examples.Features.Length][];

        Parallel.For(0, rows.Length, e => rows[e] = model.Probabilities(examples.Features[e]));

        return rows;
    }
}
