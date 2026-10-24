using System.Text.Json;
using System.Text.Json.Nodes;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class ContractFilesTests : IDisposable
{
    private static readonly DateTime Created = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<string, string> NoGiven = [];

    private static readonly Term[] Terms = [new LinearTerm("Gaussian"), new LinearTerm("Indicator"), new LinearSplineTerm("Uniform", 5)];

    private static readonly ModelDeclaration Declaration = new(new BernoulliDistribution(), new Dictionary<string, Term[]> { ["probability"] = Terms }, NoGiven);

    private static readonly PredictorFit Probability = new(
        -0.12,
        new Dictionary<string, TermFit>
        {
            ["Gaussian"] = new LinearFit(0.31, 8.4, [0.021]),
            ["Indicator"] = new LinearFit(0.38, 0.49, [-0.18]),
            ["Uniform"] = new LinearSplineFit([0, 8, 24], [3.1, 6.2], [2.5, 4.4], [0.07, -0.02]),
        });

    private static readonly FittedModel Fitted = new("Example", Declaration, new Dictionary<string, PredictorFit> { ["probability"] = Probability });

    private static readonly TrainingDataManifest Manifest = new(
        "Example",
        "source",
        Created,
        ["Gaussian", "Indicator", "Uniform"],
        412345,
        Declaration);

    private static readonly TrainingReport Report = new(
        "Example",
        Created,
        3067,
        new TrainingAndValidation<int>(330112, 82233),
        new TrainingAndValidation<double>(0.6712, 0.6731),
        Route.RidgePath,
        1e-4,
        [new Attempt(Route.Direct, 0, new TrainingAndValidation<double>(0.6709, 0.6738), 30), new Attempt(Route.RidgePath, 1e-4, new TrainingAndValidation<double>(0.6712, 0.6731), 21)],
        [new Failure(Route.RidgePath, 1e-5, "Example did not converge at ridge 1e-05")],
        new Optimiser(OptimiserName.Lbfgs, 41));

    private readonly string _directory = Directory.CreateTempSubdirectory("gamegraph-contract-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AManifestRoundTrips()
    {
        var path = PathOf("manifest.json");

        ContractFiles.WriteTrainingDataManifest(path, Manifest);
        var read = ContractFiles.ReadTrainingDataManifest(path);

        Assert.Equal(Manifest.Node, read.Node);
        Assert.Equal(Manifest.CreatedUtc, read.CreatedUtc);
        Assert.Equal(Manifest.Features, read.Features);
        Assert.Equal(Manifest.Model.Terms["probability"], read.Model.Terms["probability"]);
        AssertRewrittenIdentically(path, p => ContractFiles.WriteTrainingDataManifest(p, read));
    }

    [Fact]
    public void AFittedModelRoundTrips()
    {
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);
        var read = ContractFiles.ReadFittedModel(path);

        var probability = read.Fit["probability"];

        Assert.Equal(Probability.Intercept, probability.Intercept);
        var indicator = Assert.IsType<LinearFit>(probability.Terms["Indicator"]);

        Assert.Equal((0.38, 0.49), (indicator.Centre, indicator.Scale));
        Assert.Equal([-0.18], indicator.Coefficients);
        Assert.Equal(Terms, read.Model.Terms["probability"]);
        AssertRewrittenIdentically(path, p => ContractFiles.WriteFittedModel(p, read));
    }

    [Fact]
    public void ATrainingReportRoundTrips()
    {
        var path = PathOf("report.json");

        ContractFiles.WriteTrainingReport(path, Report);
        var read = ContractFiles.ReadTrainingReport(path);

        Assert.Equal(Report.Examples, read.Examples);
        Assert.Equal(Report.NegativeLogLikelihood, read.NegativeLogLikelihood);
        Assert.Equal(Report.Optimiser, read.Optimiser);
        Assert.Equal((Report.Route, Report.Ridge), (read.Route, read.Ridge));
        Assert.Equal(Report.Attempts, read.Attempts);
        Assert.Equal(Report.Failures, read.Failures);
        AssertRewrittenIdentically(path, p => ContractFiles.WriteTrainingReport(p, read));
    }

    [Fact]
    public void AWrittenDocumentCarriesTheSchemaVersion()
    {
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);

        Assert.Equal(1, Document(path)["schemaVersion"]!.GetValue<int>());
    }

    public static TheoryData<string> Breakages => new(Broken.Keys);

    [Theory]
    [MemberData(nameof(Breakages))]
    public void ReadingAFittedModelThatBreaksTheSchemaFails(string breakage)
    {
        var path = WrittenThenChanged(Broken[breakage]);

        var error = Assert.Throws<InvalidDataException>(() => ContractFiles.ReadFittedModel(path));
        Assert.Contains("does not match fitted-model.schema.json", error.Message);
    }

    public static TheoryData<string> Mismatches => new(Mismatched.Keys);

    [Theory]
    [MemberData(nameof(Mismatches))]
    public void ReadingAFitThatDoesNotMatchItsDeclarationFails(string mismatch)
    {
        var (change, message) = Mismatched[mismatch];
        var path = WrittenThenChanged(change);

        var error = Assert.Throws<ArgumentException>(() => ContractFiles.ReadFittedModel(path));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void ASplineFitOnALinearTermFails()
    {
        var terms = new Dictionary<string, TermFit>(Probability.Terms) { ["Gaussian"] = Probability.Terms["Uniform"] };

        var error = Assert.Throws<ArgumentException>(() => new FittedModel("Example", Declaration, new Dictionary<string, PredictorFit> { ["probability"] = Probability with { Terms = terms } }));

        Assert.Contains("The fit of Gaussian does not match its basis", error.Message);
    }

    [Fact]
    public void ReadingANumberThatIsNotFiniteFails()
    {
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);
        File.WriteAllText(path, File.ReadAllText(path).Replace("-0.12", "NaN"));

        Assert.ThrowsAny<JsonException>(() => ContractFiles.ReadFittedModel(path));
    }

    [Fact]
    public void WritingANumberThatIsNotFiniteFails()
    {
        var broken = new FittedModel(Fitted.Node, Declaration, new Dictionary<string, PredictorFit> { ["probability"] = Probability with { Intercept = double.NaN } });

        Assert.Throws<ArgumentException>(() => ContractFiles.WriteFittedModel(PathOf("model.json"), broken));
    }

    [Fact]
    public void AManifestWhoseModelUsesAnUnknownFeatureFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingDataManifest("Example", "source", Created, ["Gaussian"], 1, Declaration));

        Assert.Contains("not among its features", error.Message);
    }

    [Fact]
    public void AManifestWithAFeatureItsModelDoesNotUseFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingDataManifest("Example", "source", Created, ["Gaussian", "Indicator", "Uniform", "Unused"], 1, Declaration));

        Assert.Contains("that its model does not use", error.Message);
    }

    [Fact]
    public void ASplineWhoseKnotsDoNotIncreaseFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new LinearSplineFit([0, 0, 1], [0, 0], [1, 1], [0, 0]));

        Assert.Contains("not strictly increasing", error.Message);
    }

    [Fact]
    public void ASplineWithTheWrongNumberOfCoefficientsFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new LinearSplineFit([0, 1, 2], [0, 0], [1, 1], [0]));

        Assert.Contains("needs 2 centres, scales and coefficients", error.Message);
    }

    [Fact]
    public void ACategoricalModelRoundTrips()
    {
        var path = PathOf("model.json");
        var categorical = new FittedModel(
            "Example",
            new ModelDeclaration(
                new CategoricalDistribution(["first", "second", "third"]),
                new Dictionary<string, Term[]> { ["second"] = Terms, ["third"] = Terms[..1] },
                NoGiven),
            new Dictionary<string, PredictorFit>
            {
                ["second"] = Probability,
                ["third"] = new(-1.1, new Dictionary<string, TermFit> { ["Gaussian"] = new LinearFit(0.31, 8.4, [0.5]) }),
            });

        ContractFiles.WriteFittedModel(path, categorical);
        var read = ContractFiles.ReadFittedModel(path);

        Assert.Equal(["first", "second", "third"], Assert.IsType<CategoricalDistribution>(read.Model.Distribution).Categories);
        AssertRewrittenIdentically(path, p => ContractFiles.WriteFittedModel(p, read));
    }

    [Fact]
    public void PredictedParametersThatAreNotTheDistributionsFail()
    {
        var error = Assert.Throws<ArgumentException>(() => new ModelDeclaration(
            new CategoricalDistribution(["first", "second"]),
            new Dictionary<string, Term[]> { ["third"] = [] },
            NoGiven));

        Assert.Contains("predicts [second], got [third]", error.Message);
    }

    [Fact]
    public void APredictedParameterGivenInsteadFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new ModelDeclaration(
            new BernoulliDistribution(),
            new Dictionary<string, Term[]>(),
            new Dictionary<string, string> { ["probability"] = "Gaussian" }));

        Assert.Contains("predicts [probability], got []", error.Message);
    }

    [Fact]
    public void AMissingGivenParameterFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new ModelDeclaration(
            new DiscreteJohnsonSuDistribution(),
            new Dictionary<string, Term[]> { ["location"] = [], ["scale"] = [], ["skew"] = [], ["tail"] = [] },
            NoGiven));

        Assert.Contains("is given [maximum], got []", error.Message);
    }

    [Fact]
    public void AJohnsonSuModelWithAGivenMaximumRoundTrips()
    {
        var path = PathOf("model.json");
        var none = new Dictionary<string, TermFit>();
        var johnsonSu = new FittedModel(
            "Example",
            new ModelDeclaration(
                new DiscreteJohnsonSuDistribution(),
                new Dictionary<string, Term[]> { ["location"] = Terms[..1], ["scale"] = [], ["skew"] = [], ["tail"] = [] },
                new Dictionary<string, string> { ["maximum"] = "Bound" }),
            new Dictionary<string, PredictorFit>
            {
                ["location"] = new(9, new Dictionary<string, TermFit> { ["Gaussian"] = new LinearFit(0.31, 8.4, [2]) }),
                ["scale"] = new(1.5, none),
                ["skew"] = new(0.2, none),
                ["tail"] = new(0.4, none),
            });

        ContractFiles.WriteFittedModel(path, johnsonSu);
        var read = ContractFiles.ReadFittedModel(path);

        Assert.Equal("Bound", read.Model.Given["maximum"]);
        Assert.Equal(["Gaussian", "Bound"], read.Model.Features());
        AssertRewrittenIdentically(path, p => ContractFiles.WriteFittedModel(p, read));
    }

    [Fact]
    public void ACategoricalDistributionWithOneCategoryFails() =>
        Assert.Throws<ArgumentException>(() => new CategoricalDistribution(["first"]));

    [Fact]
    public void TheSameTermTwiceFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new ModelDeclaration(
            new BernoulliDistribution(),
            new Dictionary<string, Term[]> { ["probability"] = [new LinearTerm("Gaussian"), new LinearTerm("Gaussian")] },
            NoGiven));

        Assert.Contains("appear more than once", error.Message);
    }

    [Fact]
    public void ATimeThatIsNotUtcFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingReport("Example", DateTime.Now, 1, Report.Examples, Report.NegativeLogLikelihood, Report.Route, Report.Ridge, Report.Attempts, Report.Failures, Report.Optimiser));

        Assert.Contains("not in UTC", error.Message);
    }

    private static readonly Dictionary<string, Action<JsonObject>> Broken = new()
    {
        ["an extra field"] = d => d["extra"] = 1,
        ["a missing node"] = d => d.Remove("node"),
        ["another schema version"] = d => d["schemaVersion"] = 2,
        ["an unknown distribution"] = d => d["model"]!["distribution"]!["name"] = "poisson",
        ["a categorical distribution without categories"] = d => d["model"]!["distribution"] = new JsonObject { ["name"] = "categorical" },
        ["a link in the file"] = d => ProbabilityOf(d)["link"] = "identity",
        ["a given parameter that is not a feature name"] = d => d["model"]!["given"] = new JsonObject { ["maximum"] = 3 },
        ["a zero scale"] = d => TermFitOf(d, "Gaussian")["scale"] = 0,
        ["two coefficients on a linear term"] = d => TermFitOf(d, "Gaussian")["coefficients"] = new JsonArray(1, 2),
        ["an unknown basis"] = d => d["model"]!["terms"]!["probability"]![0]!["basis"] = "spline",
    };

    private static readonly Dictionary<string, (Action<JsonObject> Change, string Message)> Mismatched = new()
    {
        ["an unknown parameter"] = (d => d["fit"]!["mean"] = ProbabilityOf(d).DeepClone(), "mean is not a declared predicted parameter"),
        ["a missing parameter"] = (d => d["fit"]!.AsObject().Remove("probability"), "predicts [probability], but its fit has []"),
        ["an undeclared term"] = (d => ProbabilityOf(d)["terms"]!["Extra"] = TermFitOf(d, "Gaussian").DeepClone(), "Extra is not a declared term"),
        ["a missing term"] = (d => ProbabilityOf(d)["terms"]!.AsObject().Remove("Indicator"), "fitted [Gaussian, Uniform]"),
        ["more knot positions than knots"] = (d => d["model"]!["terms"]!["probability"]![2]!["knots"] = 2, "with 2 knots has 3 positions"),
    };

    private static JsonNode ProbabilityOf(JsonObject document) => document["fit"]!["probability"]!;

    private static JsonNode TermFitOf(JsonObject document, string feature) => ProbabilityOf(document)["terms"]![feature]!;

    private static JsonObject Document(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private string WrittenThenChanged(Action<JsonObject> change)
    {
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);
        var document = Document(path);
        change(document);
        File.WriteAllText(path, document.ToJsonString());

        return path;
    }

    private string PathOf(string name) => Path.Combine(_directory, name);

    private void AssertRewrittenIdentically(string path, Action<string> rewrite)
    {
        var again = PathOf("again.json");

        rewrite(again);

        Assert.Equal(File.ReadAllText(path), File.ReadAllText(again));
    }
}
