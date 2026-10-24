namespace GameGraph.Engine;

public delegate Next<TState> Transition<TState>(TState state, int outcome);
