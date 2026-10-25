namespace GameGraph.Models.Contract;

public sealed record TrainingDataFile(TrainingDataManifest Manifest, string[] GameIds, int[] GameExamples, double[][] Features, int[] Outcomes)
{
    public int ExamplesIn(int games) => GameExamples.Take(games).Sum();
}
