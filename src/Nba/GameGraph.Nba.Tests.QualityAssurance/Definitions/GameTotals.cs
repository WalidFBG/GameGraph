namespace GameGraph.Nba.Tests.QualityAssurance.Definitions;

internal sealed record GameTotals(int HomePoints, int AwayPoints, int Possessions, int Periods, IReadOnlyDictionary<int, PlayerTotals> Players);
