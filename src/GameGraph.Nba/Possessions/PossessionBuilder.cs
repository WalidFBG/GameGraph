using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Possessions;

internal sealed class PossessionBuilder
{
    private readonly List<Possession> possessions = [];
    private TeamType team = TeamType.NotSet;
    private int madeFieldGoals;

    public void Act(TeamType actingTeam, bool madeFieldGoal)
    {
        if (actingTeam != team)
        {
            End();
            team = actingTeam;
        }

        if (madeFieldGoal)
            madeFieldGoals++;
    }

    public void End()
    {
        if (team == TeamType.NotSet)
            return;

        possessions.Add(new Possession(team, madeFieldGoals));
        team = TeamType.NotSet;
        madeFieldGoals = 0;
    }

    public Possession[] Build()
    {
        End();
        return possessions.ToArray();
    }
}
