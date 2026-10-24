using System.Text.Json;
using System.Text.Json.Nodes;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class ContractFilesTests : IDisposable
{
    private static readonly DateTime Created = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static readonly FittedPredictor Probability = new(
        -0.12,
        [
            new FittedLinearTerm("Gaussian", 0.31, 8.4, [0.021]),
            new FittedLinearTerm("Indicator", 0.38, 0.49, [-0.18]),
            new FittedLinearSplineTerm("Uniform", 5, [0, 8, 24], [3.1, 6.2], [2.5, 4.4], [0.07, -0.02]),
        ]);

    private static readonly FittedModel Fitted = new("Example", new BernoulliDistribution(), new Dictionary<string, FittedPredictor> { ["probability"] = Probability }, new Dictionary<string, string>());

    private static readonly TrainingDataManifest Manifest = new(
        "Example",
        "source",
        Created,
        ["Gaussian", "Indicator", "Uniform"],
        412345,
        Fitted.Declaration());

    private static readonly TrainingReport Report = new(
        "Example",
        Created,
        3067,
        new TrainingAndValidation<int>(330112, 82233),
        new TrainingAndValidation<double>(0.6712, 0.6731),
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

        var probability = read.Predictors["probability"];

        Assert.Equal(Probability.Intercept, probability.Intercept);
        Assert.Equal(Probability.Terms[1].Coefficients, probability.Terms[1].Coefficients);
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
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);
        var document = Document(path);
        Broken[breakage](document);
        File.WriteAllText(path, document.ToJsonString());

        var error = Assert.Throws<InvalidDataException>(() => ContractFiles.ReadFittedModel(path));
        Assert.Contains("does not match fitted-model.schema.json", error.Message);
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
        var broken = new FittedModel(
            Fitted.Node,
            Fitted.Distribution,
            new Dictionary<string, FittedPredictor> { ["probability"] = new FittedPredictor(double.NaN, Probability.Terms) },
            new Dictionary<string, string>());

        Assert.Throws<ArgumentException>(() => ContractFiles.WriteFittedModel(PathOf("model.json"), broken));
    }

    [Fact]
    public void AManifestWhoseModelUsesAnUnknownFeatureFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingDataManifest("Example", "source", Created, ["Gaussian"], 1, Fitted.Declaration()));

        Assert.Contains("not among its features", error.Message);
    }

    [Fact]
    public void AManifestWithAFeatureItsModelDoesNotUseFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingDataManifest("Example", "source", Created, ["Gaussian", "Indicator", "Uniform", "Unused"], 1, Fitted.Declaration()));

        Assert.Contains("that its model does not use", error.Message);
    }

    [Fact]
    public void ASplineWhoseKnotsDoNotIncreaseFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new FittedLinearSplineTerm("x", 3, [0, 0, 1], [0, 0], [1, 1], [0, 0]));

        Assert.Contains("not strictly increasing", error.Message);
    }

    [Fact]
    public void ASplineWithTheWrongNumberOfCoefficientsFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new FittedLinearSplineTerm("x", 3, [0, 1, 2], [0, 0], [1, 1], [0]));

        Assert.Contains("needs 2 centres, scales and coefficients", error.Message);
    }

    [Fact]
    public void AFittedModelGivesBackItsDeclaration()
    {
        Assert.Equal(
            [new LinearTerm("Gaussian"), new LinearTerm("Indicator"), new LinearSplineTerm("Uniform", 5)],
            Fitted.Declaration().Terms["probability"]);
    }

    [Fact]
    public void ACategoricalModelRoundTrips()
    {
        var path = PathOf("model.json");
        var categorical = new FittedModel(
            "Example",
            new CategoricalDistribution(["first", "second", "third"]),
            new Dictionary<string, FittedPredictor>
            {
                ["second"] = new FittedPredictor(0.4, Probability.Terms),
                ["third"] = new FittedPredictor(-1.1, Probability.Terms[..1]),
            },
            new Dictionary<string, string>());

        ContractFiles.WriteFittedModel(path, categorical);
        var read = ContractFiles.ReadFittedModel(path);

        Assert.Equal(["first", "second", "third"], Assert.IsType<CategoricalDistribution>(read.Distribution).Categories);
        AssertRewrittenIdentically(path, p => ContractFiles.WriteFittedModel(p, read));
    }

    [Fact]
    public void PredictedParametersThatAreNotTheDistributionsFail()
    {
        var error = Assert.Throws<ArgumentException>(() => new ModelDeclaration(
            new CategoricalDistribution(["first", "second"]),
            new Dictionary<string, Term[]> { ["third"] = [] },
            new Dictionary<string, string>()));

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
            new Dictionary<string, string>()));

        Assert.Contains("is given [maximum], got []", error.Message);
    }

    [Fact]
    public void ReadingAFittedModelWithAnUnknownParameterFails()
    {
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);
        var document = Document(path);
        document["predictors"]!["mean"] = ProbabilityOf(document).DeepClone();
        File.WriteAllText(path, document.ToJsonString());

        var error = Assert.Throws<ArgumentException>(() => ContractFiles.ReadFittedModel(path));
        Assert.Contains("predicts [probability], got [mean, probability]", error.Message);
    }

    [Fact]
    public void AJohnsonSuModelWithAGivenMaximumRoundTrips()
    {
        var path = PathOf("model.json");
        var johnsonSu = new FittedModel(
            "Example",
            new DiscreteJohnsonSuDistribution(),
            new Dictionary<string, FittedPredictor>
            {
                ["location"] = new FittedPredictor(9, Probability.Terms[..1]),
                ["scale"] = new FittedPredictor(1.5, []),
                ["skew"] = new FittedPredictor(0.2, []),
                ["tail"] = new FittedPredictor(0.4, []),
            },
            new Dictionary<string, string> { ["maximum"] = "Bound" });

        ContractFiles.WriteFittedModel(path, johnsonSu);
        var read = ContractFiles.ReadFittedModel(path);

        Assert.Equal("Bound", read.Given["maximum"]);
        Assert.Equal(["Gaussian", "Bound"], read.Declaration().Features());
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
            new Dictionary<string, string>()));

        Assert.Contains("appear more than once", error.Message);
    }

    [Fact]
    public void ATimeThatIsNotUtcFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingReport("Example", DateTime.Now, 1, Report.Examples, Report.NegativeLogLikelihood, Report.Optimiser));

        Assert.Contains("not in UTC", error.Message);
    }

    private static readonly Dictionary<string, Action<JsonObject>> Broken = new()
    {
        ["an extra field"] = d => d["extra"] = 1,
        ["a missing node"] = d => d.Remove("node"),
        ["another schema version"] = d => d["schemaVersion"] = 2,
        ["an unknown distribution"] = d => d["distribution"]!["name"] = "poisson",
        ["a categorical distribution without categories"] = d => d["distribution"] = new JsonObject { ["name"] = "categorical" },
        ["a link in the file"] = d => ProbabilityOf(d)["link"] = "identity",
        ["a given parameter that is not a feature name"] = d => d["given"] = new JsonObject { ["maximum"] = 3 },
        ["a zero scale"] = d => FirstTerm(d)["scale"] = 0,
        ["two coefficients on a linear term"] = d => FirstTerm(d)["coefficients"] = new JsonArray(1, 2),
        ["an unknown basis"] = d => FirstTerm(d)["basis"] = "spline",
    };

    private static JsonNode ProbabilityOf(JsonObject document) => document["predictors"]!["probability"]!;

    private static JsonNode FirstTerm(JsonObject document) => ProbabilityOf(document)["terms"]![0]!;

    private static JsonObject Document(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private string PathOf(string name) => Path.Combine(_directory, name);

    private void AssertRewrittenIdentically(string path, Action<string> rewrite)
    {
        var again = PathOf("again.json");

        rewrite(again);

        Assert.Equal(File.ReadAllText(path), File.ReadAllText(again));
    }
}
