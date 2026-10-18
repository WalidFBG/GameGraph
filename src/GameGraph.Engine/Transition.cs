namespace GameGraph.Engine;

public delegate NodeSpec<TState, TIncidentCollector> Transition<TState, TIncidentCollector>(TState state, int outcome, TIncidentCollector incidentCollector);
