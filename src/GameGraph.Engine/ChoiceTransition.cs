namespace GameGraph.Engine;

public delegate Next<TState> ChoiceTransition<TState>(TState state, ReadOnlySpan<int> chosen);
