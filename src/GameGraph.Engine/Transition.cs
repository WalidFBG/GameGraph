namespace GameGraph.Engine;

public delegate Next<TState, TIncidentCollector> Transition<TState, TIncidentCollector>(TState state, int outcome, TIncidentCollector incidentCollector);
