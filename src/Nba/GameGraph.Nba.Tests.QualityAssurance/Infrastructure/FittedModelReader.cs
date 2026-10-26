using GameGraph.Contract.Files;
using GameGraph.Contract;
using GameGraph.Nba.Graph;
using GameGraph.Simulation;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class FittedModelReader
{
    public static FittedModel Read(string node) => ContractDocuments.ReadFittedModel(DataDirectories.FittedModel(node));

    public static FittedGraph<NbaState> ReadGame() => new(NbaGraph.Instance, [.. NbaGraph.Instance.Nodes.Select(n => Read(n.Name))]);
}
