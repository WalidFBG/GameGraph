using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaGraph
{
    public static Graph<NbaState> Instance { get; } = NbaNodes.Builder.Build(NbaNodes.Starters);
}
