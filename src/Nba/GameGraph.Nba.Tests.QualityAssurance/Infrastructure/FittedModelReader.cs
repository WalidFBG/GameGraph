using GameGraph.Models.Contract;
using GameGraph.Nba.Graph;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class FittedModelReader
{
    public static FittedModel Read(string node) => ContractFiles.ReadFittedModel(DataDirectories.FittedModel(node));

    public static FittedGame<NbaState> ReadGame() => new(NbaGameSpec.Instance, [.. NbaGameSpec.Instance.Nodes.Select(n => Read(n.Name))]);
}
