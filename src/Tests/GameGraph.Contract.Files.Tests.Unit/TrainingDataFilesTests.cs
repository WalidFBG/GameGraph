using GameGraph.Contract.Distributions;
using GameGraph.Contract.Terms;
using Xunit;

namespace GameGraph.Contract.Files.Tests.Unit;

public sealed class TrainingDataFilesTests : IDisposable
{
    private static readonly DateTime Created = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<string, string> NoGiven = [];

    private static readonly ModelDeclaration Bernoulli = new(
        new BernoulliDistribution(),
        new Dictionary<string, Term[]> { ["probability"] = [new LinearTerm("Gaussian"), new LinearTerm("Indicator")] },
        NoGiven);

    private static readonly ModelDeclaration Choice = new(
        new ChoiceDistribution(),
        new Dictionary<string, Term[]> { ["utility"] = [new LinearTerm("Gaussian"), new LinearTerm("Indicator")] },
        new Dictionary<string, string> { [ChoiceDistribution.Size] = "Size" });

    private readonly string _directory = Path.Combine(Directory.CreateTempSubdirectory("gamegraph-training-data-").FullName, "Example");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_directory)!, recursive: true);

    [Fact]
    public void TrainingDataRoundTrips()
    {
        var data = Data(Bernoulli, [[new([0.5, 1], 1), new([-1.25, 0], 0)], [new([3, 1], 0)]]);

        TrainingDataFiles.Write(_directory, data);

        AssertSame(data, TrainingDataFiles.Read(_directory));
        Assert.Equal(["Gaussian,Indicator,outcome", "0.5,1,1", "-1.25,0,0", "3,1,0"], File.ReadAllLines(Path.Combine(_directory, "examples.csv")));
    }

    [Fact]
    public void AChoiceWritesOneRowPerCandidateMarkingEachChosenOneAndRoundTrips()
    {
        var data = Data(Choice, [[new([0.5, 1, 1, 2, 0, 1, -1, 1, 1], 0b010)], [new([3, 1, 2, 4, 0, 2], 0b11)]]);

        TrainingDataFiles.Write(_directory, data);

        AssertSame(data, TrainingDataFiles.Read(_directory));
        Assert.Equal(
            ["decision,Gaussian,Indicator,Size,outcome", "0,0.5,1,1,0", "0,2,0,1,1", "0,-1,1,1,0", "1,3,1,2,1", "1,4,0,2,1"],
            File.ReadAllLines(Path.Combine(_directory, "examples.csv")));
    }

    [Fact]
    public void WritingReplacesTheDirectory()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "stale.csv"), "");

        TrainingDataFiles.Write(_directory, Data(Bernoulli, [[new([0.5, 1], 1)]]));

        Assert.False(File.Exists(Path.Combine(_directory, "stale.csv")));
    }

    [Fact]
    public void ReadingADecisionThatChoosesMoreCandidatesThanItsSizeFails()
    {
        TrainingDataFiles.Write(_directory, Data(Choice, [[new([0.5, 1, 1, 2, 0, 1], 0b01)]]));
        var path = Path.Combine(_directory, "examples.csv");
        File.WriteAllLines(path, [.. File.ReadAllLines(path).Select(l => l.EndsWith(",0") ? l[..^1] + "1" : l)]);

        var error = Assert.Throws<ArgumentException>(() => TrainingDataFiles.Read(_directory));

        Assert.Contains("chooses 2 candidates, but its size is 1", error.Message);
    }

    [Fact]
    public void AnExampleThatIsNotWholeRowsOfFeaturesFails()
    {
        var error = Assert.Throws<ArgumentException>(() => Data(Choice, [[new([0.5, 1, 1, 2], 0b1)]]));

        Assert.Contains("not 1 to 31 candidates of 3 features", error.Message);
    }

    [Fact]
    public void AChoiceOfACandidateThatIsNotThereFails()
    {
        var error = Assert.Throws<ArgumentException>(() => Data(Choice, [[new([0.5, 1, 1, 2, 0, 1], 0b100)]]));

        Assert.Contains("not a set of its 2 candidates' rows", error.Message);
    }

    [Fact]
    public void AChoiceOfNoCandidateFails()
    {
        var error = Assert.Throws<ArgumentException>(() => Data(Choice, [[new([0.5, 1, 1, 2, 0, 1], 0)]]));

        Assert.Contains("not a set of its 2 candidates' rows", error.Message);
    }

    [Fact]
    public void AGameWhoseStartIsNotUtcFails()
    {
        var error = Assert.Throws<ArgumentException>(() => new GameExamples("g0", DateTime.Now, []));

        Assert.Contains("not in UTC", error.Message);
    }

    [Fact]
    public void GamesThatDoNotHoldTheManifestsExamplesFail()
    {
        var manifest = new TrainingDataManifest("Example", "source", Created, ["Gaussian", "Indicator"], 2, Bernoulli);

        var error = Assert.Throws<ArgumentException>(() => new TrainingData(manifest, [new GameExamples("g0", Created, [new([0.5, 1], 1)])]));

        Assert.Contains("says 2 examples, its games hold 1", error.Message);
    }

    private static TrainingData Data(ModelDeclaration model, TrainingExample[][] games)
    {
        var manifest = new TrainingDataManifest("Example", "source", Created, [.. model.Features()], games.Sum(g => g.Length), model);

        return new TrainingData(manifest, [.. games.Select((examples, g) => new GameExamples($"g{g}", Created.AddDays(g), examples))]);
    }

    private static void AssertSame(TrainingData expected, TrainingData actual)
    {
        Assert.Equal(expected.Manifest.Model, actual.Manifest.Model);
        Assert.Equal(expected.Manifest.Features, actual.Manifest.Features);
        Assert.Equal(expected.Games.Select(g => (g.GameId, g.StartUtc)), actual.Games.Select(g => (g.GameId, g.StartUtc)));
        Assert.Equal(Examples(expected), Examples(actual));
    }

    private static string[] Examples(TrainingData data) =>
        [.. data.Games.SelectMany(g => g.Examples).Select(e => $"{string.Join(' ', e.Features)} -> {e.Outcome}")];
}
