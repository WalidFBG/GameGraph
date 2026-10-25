namespace GameGraph.Nba.Tests.QualityAssurance.Definitions;

internal sealed record RecentGameResult(
    string Game,
    int Home,
    int Away,
    int Possessions,
    bool Overtime,
    double SimulatedHome,
    double SimulatedAway,
    double SimulatedPossessions,
    double HomeWin,
    double SimulatedOvertime);
