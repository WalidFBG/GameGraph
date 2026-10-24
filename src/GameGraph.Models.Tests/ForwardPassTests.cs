using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class ForwardPassTests
{
    private static readonly FittedModel Fitted = new(
        "Golden",
        Distribution.Bernoulli,
        new Dictionary<string, FittedPredictedParameter>
        {
            ["probability"] = new(
                Link.Logit,
                -0.12,
                [new FittedLinearTerm("ScoreMargin", 0.31, 8.4, [0.021]), new FittedLinearTerm("ShotIsThree", 0.38, 0.49, [-0.18])]),
        });

    private static readonly string[] Features = ["ScoreMargin", "ShotIsThree"];

    [Fact]
    public void ALinearTermCentresAndScalesItsFeature()
    {
        var term = LinearColumns.FromFitted(new FittedLinearTerm("b", 1, 2, [0.5]), ["a", "b"]);

        Assert.Equal(2.0, term.Expand([9, 5]));
        Assert.Equal(-1.0, term.Expand([9, -1]));
    }

    [Fact]
    public void ALinearTermWhoseFeatureIsMissingFails()
    {
        var error = Assert.Throws<ArgumentException>(() => LinearColumns.FromFitted(new FittedLinearTerm("c", 0, 1, [0.5]), ["a", "b"]));

        Assert.Contains("No feature c", error.Message);
    }

    [Fact]
    public void TheInverseLogitIsTheSigmoid()
    {
        Assert.Equal(0.5, Links.Inverse(Link.Logit, 0));
        Assert.Equal(0.75, Links.Inverse(Link.Logit, Math.Log(3)), 1e-15);
    }

    [Fact]
    public void BernoulliGivesTheLogProbabilityOfEachOutcome()
    {
        Assert.Equal(Math.Log(0.25), Bernoulli.LogProbability([0.25], 1), 1e-15);
        Assert.Equal(Math.Log(0.75), Bernoulli.LogProbability([0.25], 0), 1e-15);
    }

    [Fact]
    public void BernoulliSamplesOneBelowTheProbability()
    {
        Assert.Equal(1, Bernoulli.Sample([0.25], 0));
        Assert.Equal(1, Bernoulli.Sample([0.25], 0.2499));
        Assert.Equal(0, Bernoulli.Sample([0.25], 0.25));
    }

    [Fact]
    public void BernoulliRejectsAnOutcomeThatIsNot0Or1() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Bernoulli.LogProbability([0.5], 2));

    [Fact]
    public void BernoulliRejectsAUniformOutside0To1() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Bernoulli.Sample([0.5], 1));

    [Fact]
    public void TheModelCombinesTermsLinkAndDistribution()
    {
        var model = Model.FromFitted(Fitted, Features);
        var expected = 1 / (1 + Math.Exp(-(-0.12 + 0.021 * (4 - 0.31) / 8.4 - 0.18 * (1 - 0.38) / 0.49)));

        Assert.Equal(expected, model.ParameterValues([4, 1])[0], 1e-15);
        Assert.Equal(Math.Log(expected), model.LogProbability([4, 1], 1), 1e-15);
    }

    [Fact]
    public void AModelWithTheWrongParametersFails()
    {
        var wrong = Fitted with { Parameters = new Dictionary<string, FittedPredictedParameter> { ["mean"] = Fitted.Parameters["probability"] } };

        Assert.Throws<ArgumentException>(() => Model.FromFitted(wrong, Features));
    }
}
