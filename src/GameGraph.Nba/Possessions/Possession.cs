using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Possessions;

public readonly record struct Possession(TeamType Team, int MadeFieldGoals)
{
    public bool Scored => MadeFieldGoals > 0;
}
