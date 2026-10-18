using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaGameSpec
{
    public static GameSpec<NbaState> Instance { get; } = new([NbaNodes.Possession], NbaNodes.Possession);
}
