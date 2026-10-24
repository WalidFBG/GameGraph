using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class ForwardPassTests
{
    private static readonly FittedModel Fitted = new(
        "Golden",
        new ModelDeclaration(
            new BernoulliDistribution(),
            new Dictionary<string, Term[]> { ["probability"] = [new LinearTerm("Gaussian"), new LinearTerm("Indicator")] },
            new Dictionary<string, string>()),
        new Dictionary<string, PredictorFit>
        {
            ["probability"] = new(
                -0.12,
                new Dictionary<string, TermFit> { ["Gaussian"] = new LinearFit(0.31, 8.4, [0.021]), ["Indicator"] = new LinearFit(0.38, 0.49, [-0.18]) }),
        });

    private static readonly string[] Features = ["Gaussian", "Indicator"];

    [Fact]
    public void ALinearTermCentresAndScalesItsFeature()
    {
        var term = LinearColumns.FromFit("b", new LinearFit(1, 2, [0.5]), ["a", "b"]);

        Span<double> columns = stackalloc double[1];

        term.Expand([9, 5], columns);
        Assert.Equal(2.0, columns[0]);

        term.Expand([9, -1], columns);
        Assert.Equal(-1.0, columns[0]);
    }

    [Fact]
    public void ALinearSplineTermMeasuresEachSegmentAndIsFlatBeyondItsEndKnots()
    {
        var term = LinearSplineColumns.FromFit("x", new LinearSplineFit([0, 10, 20], [0, 0], [1, 1], [0, 0]), ["x"]);
        Span<double> columns = stackalloc double[2];

        foreach (var (value, first, second) in new[] { (-5.0, 0.0, 0.0), (0.0, 0.0, 0.0), (4.0, 4.0, 0.0), (10.0, 10.0, 0.0), (15.0, 10.0, 5.0), (20.0, 10.0, 10.0), (99.0, 10.0, 10.0) })
        {
            term.Expand([value], columns);

            Assert.Equal(first, columns[0]);
            Assert.Equal(second, columns[1]);
        }
    }

    [Fact]
    public void ALinearSplineTermCentresAndScalesEachSegment()
    {
        var term = LinearSplineColumns.FromFit("x", new LinearSplineFit([0, 10, 20], [2, 1], [4, 0.5], [0, 0]), ["x"]);
        Span<double> columns = stackalloc double[2];

        term.Expand([12], columns);

        Assert.Equal((10 - 2) / 4.0, columns[0]);
        Assert.Equal((2 - 1) / 0.5, columns[1]);
    }

    [Fact]
    public void ALinearTermWhoseFeatureIsMissingFails()
    {
        var error = Assert.Throws<ArgumentException>(() => LinearColumns.FromFit("c", new LinearFit(0, 1, [0.5]), ["a", "b"]));

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
        Assert.Equal(Math.Log(0.25), new Bernoulli().LogProbability([0.25], 1), 1e-15);
        Assert.Equal(Math.Log(0.75), new Bernoulli().LogProbability([0.25], 0), 1e-15);
    }

    [Fact]
    public void BernoulliSamplesOneBelowTheProbability()
    {
        Assert.Equal(1, new Bernoulli().Sample([0.25], 0));
        Assert.Equal(1, new Bernoulli().Sample([0.25], 0.2499));
        Assert.Equal(0, new Bernoulli().Sample([0.25], 0.25));
    }

    [Fact]
    public void BernoulliRejectsAnOutcomeThatIsNot0Or1() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bernoulli().LogProbability([0.5], 2));

    [Fact]
    public void BernoulliRejectsAUniformOutside0To1() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bernoulli().Sample([0.5], 1));

    [Fact]
    public void TheModelCombinesTermsLinkAndDistribution()
    {
        var model = Model.FromFitted(Fitted, Features);
        var expected = 1 / (1 + Math.Exp(-(-0.12 + 0.021 * (4 - 0.31) / 8.4 - 0.18 * (1 - 0.38) / 0.49)));
        var values = new double[1];

        model.ParameterValues([4, 1], values);

        Assert.Equal(expected, values[0], 1e-15);
        Assert.Equal(Math.Log(expected), model.LogProbability([4, 1], 1), 1e-15);
    }

    [Fact]
    public void TheModelRejectsTheWrongNumberOfParameterValues() =>
        Assert.Throws<ArgumentException>(() => Model.FromFitted(Fitted, Features).ParameterValues([4, 1], new double[2]));

    [Fact]
    public void AModelWithTheWrongParametersFails()
    {
        Assert.Throws<ArgumentException>(() => new FittedModel(
            Fitted.Node,
            Fitted.Model,
            new Dictionary<string, PredictorFit> { ["mean"] = Fitted.Fit["probability"] }));
    }

    [Fact]
    public void CategoricalGivesTheLogProbabilityOfEachCategory()
    {
        var categorical = new Categorical(3);
        double[] logOdds = [Math.Log(2), Math.Log(3)];

        Assert.Equal(Math.Log(1 / 6.0), categorical.LogProbability(logOdds, 0), 1e-15);
        Assert.Equal(Math.Log(2 / 6.0), categorical.LogProbability(logOdds, 1), 1e-15);
        Assert.Equal(Math.Log(3 / 6.0), categorical.LogProbability(logOdds, 2), 1e-15);
    }

    [Fact]
    public void CategoricalSamplesTheFirstCategoryWhoseCumulativeProbabilityExceedsTheUniform()
    {
        var categorical = new Categorical(3);
        double[] logOdds = [Math.Log(2), Math.Log(3)];

        Assert.Equal(0, categorical.Sample(logOdds, 0));
        Assert.Equal(0, categorical.Sample(logOdds, 0.16));
        Assert.Equal(1, categorical.Sample(logOdds, 0.17));
        Assert.Equal(1, categorical.Sample(logOdds, 0.49));
        Assert.Equal(2, categorical.Sample(logOdds, 0.51));
    }

    [Fact]
    public void CategoricalRejectsAnOutcomeOutsideItsCategories() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Categorical(3).LogProbability([0, 0], 3));

    [Fact]
    public void CategoricalRejectsTheWrongNumberOfLogOdds() =>
        Assert.Throws<ArgumentException>(() => new Categorical(3).LogProbability([0], 0));

    [Fact]
    public void TheDiscreteJohnsonSuProbabilitiesAddUpTo1UpToTheMaximum()
    {
        var johnsonSu = new DiscreteJohnsonSu();
        double[] parameters = [6, 4, 0.5, 1.3, 10];
        var total = 0.0;

        for (var k = 0; k <= 10; k++)
            total += Math.Exp(johnsonSu.LogProbability(parameters, k));

        Assert.Equal(1, total, 1e-13);
    }

    [Fact]
    public void TheDiscreteJohnsonSuSamplesWholeNumbersWithinItsMaximum()
    {
        var johnsonSu = new DiscreteJohnsonSu();
        double[] parameters = [6, 4, 0.5, 1.3, 10];
        var previous = 0;

        for (var u = 0.0; u < 0.999; u += 0.002)
        {
            var sample = johnsonSu.Sample(parameters, u);

            Assert.InRange(sample, previous, 10);
            previous = sample;
        }

        Assert.Equal(0, johnsonSu.Sample(parameters, 0));
        Assert.Equal(10, johnsonSu.Sample(parameters, 0.9999));
    }

    [Fact]
    public void TheDiscreteJohnsonSuRejectsAnOutcomeAboveItsMaximum() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscreteJohnsonSu().LogProbability([6, 4, 0.5, 1.3, 10], 11));

    [Fact]
    public void TheDiscreteJohnsonSuRejectsAMaximumThatIsNotAWholeNumber() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscreteJohnsonSu().LogProbability([6, 4, 0.5, 1.3, 10.5], 3));

    [Fact]
    public void TheLogLinkExponentiates() => Assert.Equal(Math.Exp(1.5), Links.Inverse(Link.Log, 1.5));

    [Fact]
    public void TheIdentityLinkLeavesEtaAsItIs() => Assert.Equal(-1.25, Links.Inverse(Link.Identity, -1.25));
}
