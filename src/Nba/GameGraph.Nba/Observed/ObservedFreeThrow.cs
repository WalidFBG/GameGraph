namespace GameGraph.Nba.Observed;

public readonly record struct ObservedFreeThrow(bool Made, int[] TimeoutsBefore);
