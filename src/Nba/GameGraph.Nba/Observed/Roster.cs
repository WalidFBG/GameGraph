using GameGraph.Nba.Graph;

namespace GameGraph.Nba.Observed;

public sealed record Roster
{
    public Roster(int[][] active, int[][] starters, int[] secondsPlayed)
    {
        if (active.Length != 2 || starters.Length != 2)
            throw new ArgumentException("A roster has two teams", nameof(active));

        for (var team = 0; team < active.Length; team++)
        {
            if (active[team].Length <= Lineups.Size)
                throw new ArgumentException($"A team needs more than {Lineups.Size} active players to substitute, got {active[team].Length}", nameof(active));

            if (starters[team].Length != Lineups.Size || starters[team].Except(active[team]).Any())
                throw new ArgumentException($"A team starts {Lineups.Size} of its active players", nameof(starters));
        }

        Active = active;
        Starters = starters;
        SecondsPlayed = secondsPlayed;
    }

    public int[][] Active { get; }

    public int[][] Starters { get; }

    public int[] SecondsPlayed { get; }
}
