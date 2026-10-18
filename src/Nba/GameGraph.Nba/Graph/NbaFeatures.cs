using GameGraph.Engine;

namespace GameGraph.Nba.Graph;

public static class NbaFeatures
{
    public static readonly Feature<NbaState> ScoreMargin =
        new(nameof(ScoreMargin), s => s.Score[s.TeamInPossession] - s.Score[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> TeamScoringRate =
        new(nameof(TeamScoringRate), s => s.ScoringRate[s.TeamInPossession]);

    public static readonly Feature<NbaState> DefenceTeamFouls =
        new(nameof(DefenceTeamFouls), s => s.TeamFouls[Sides.Opponent(s.TeamInPossession)]);

    public static readonly Feature<NbaState> Period =
        new(nameof(Period), s => s.Period);

    public static readonly Feature<NbaState> ShotIsThree =
        new(nameof(ShotIsThree), s => s.ShotPoints == 3 ? 1 : 0);

    public static readonly Feature<NbaState> ShotFouled =
        new(nameof(ShotFouled), s => s.Fouled ? 1 : 0);

    public static readonly Feature<NbaState> LastFreeThrow =
        new(nameof(LastFreeThrow), s => s.FreeThrowsLeft == 1 ? 1 : 0);

    public static readonly Feature<NbaState> MissedThree =
        new(nameof(MissedThree), s => s.LastMiss == Miss.Three ? 1 : 0);

    public static readonly Feature<NbaState> MissedFreeThrow =
        new(nameof(MissedFreeThrow), s => s.LastMiss == Miss.FreeThrow ? 1 : 0);
}
