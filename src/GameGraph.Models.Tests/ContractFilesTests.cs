using System.Text.Json;
using System.Text.Json.Nodes;
using GameGraph.Models.Contract;
using Xunit;

namespace GameGraph.Models.Tests;

public sealed class ContractFilesTests : IDisposable
{
    private static readonly DateTime Created = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static readonly FittedModel Fitted = new(
        "ShotMade",
        Distribution.Bernoulli,
        new Dictionary<string, FittedPredictedParameter>
        {
            ["probability"] = new(
                Link.Logit,
                -0.12,
                [new FittedLinearTerm("ScoreMargin", 0.31, 8.4, [0.021]), new FittedLinearTerm("ShotIsThree", 0.38, 0.49, [-0.18])]),
        });

    private static readonly TrainingDataManifest Manifest = new(
        "ShotMade",
        @"C:\Data\Basketball",
        Created,
        ["ScoreMargin", "TeamScoringRate", "ShotIsThree"],
        412345,
        Fitted.Declaration());

    private static readonly TrainingReport Report = new(
        "ShotMade",
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
        Assert.Equal(Manifest.Model.Parameters["probability"].Terms, read.Model.Parameters["probability"].Terms);
        AssertRewrittenIdentically(path, p => ContractFiles.WriteTrainingDataManifest(p, read));
    }

    [Fact]
    public void AFittedModelRoundTrips()
    {
        var path = PathOf("model.json");

        ContractFiles.WriteFittedModel(path, Fitted);
        var read = ContractFiles.ReadFittedModel(path);

        Assert.Equal(Fitted.Parameters["probability"].Intercept, read.Parameters["probability"].Intercept);
        Assert.Equal(Fitted.Parameters["probability"].Terms[1].Coefficients, read.Parameters["probability"].Terms[1].Coefficients);
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
        var probability = Fitted.Parameters["probability"];
        var broken = Fitted with
        {
            Parameters = new Dictionary<string, FittedPredictedParameter> { ["probability"] = new(probability.Link, double.NaN, probability.Terms) },
        };

        Assert.Throws<ArgumentException>(() => ContractFiles.WriteFittedModel(PathOf("model.json"), broken));
    }

    [Fact]
    public void AManifestWhoseModelUsesAnUnknownFeatureFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingDataManifest("ShotMade", "source", Created, ["ScoreMargin"], 1, Fitted.Declaration()));

        Assert.Contains("not among its features", error.Message);
    }

    [Fact]
    public void TheSameTermTwiceFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new PredictedParameter(Link.Logit, [new LinearTerm("ScoreMargin"), new LinearTerm("ScoreMargin")]));

        Assert.Contains("appear more than once", error.Message);
    }

    [Fact]
    public void ATimeThatIsNotUtcFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrainingReport("ShotMade", DateTime.Now, 1, Report.Examples, Report.NegativeLogLikelihood, Report.Optimiser));

        Assert.Contains("not in UTC", error.Message);
    }

    private static readonly Dictionary<string, Action<JsonObject>> Broken = new()
    {
        ["an extra field"] = d => d["extra"] = 1,
        ["a missing node"] = d => d.Remove("node"),
        ["another schema version"] = d => d["schemaVersion"] = 2,
        ["an unknown distribution"] = d => d["distribution"] = "categorical",
        ["an unknown parameter"] = d => d["parameters"]!["mean"] = new JsonObject(),
        ["a link Bernoulli does not allow"] = d => Probability(d)["link"] = "log",
        ["a zero scale"] = d => FirstTerm(d)["scale"] = 0,
        ["two coefficients on a linear term"] = d => FirstTerm(d)["coefficients"] = new JsonArray(1, 2),
        ["an unknown basis"] = d => FirstTerm(d)["basis"] = "spline",
    };

    private static JsonNode Probability(JsonObject document) => document["parameters"]!["probability"]!;

    private static JsonNode FirstTerm(JsonObject document) => Probability(document)["terms"]![0]!;

    private static JsonObject Document(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private string PathOf(string name) => Path.Combine(_directory, name);

    private void AssertRewrittenIdentically(string path, Action<string> rewrite)
    {
        var again = PathOf("again.json");

        rewrite(again);

        Assert.Equal(File.ReadAllText(path), File.ReadAllText(again));
    }
}
