namespace GameGraph.Engine;

public delegate NodeSpec<TState> Transition<TState>(TState state, int outcome);
