# Engine review

A review of the Engine, Simulation and Models, against the goal of a generic simulator that the plan's speed work (compilation, parallelism, xoshiro256**) can make fast. Delete this file once the changes below are made.

## The verdict

- The design is sound, and most of it already fits the speed plan:
  - features are expressions a compiler can read;
  - rules are static methods;
  - `Model.Sample` takes a uniform number rather than owning randomness;
  - distributions and bases are closed sets;
  - each simulation gets its own seed.
- The gaps are in three places:
  - the engine carries the sport's incident type everywhere it goes;
  - there's no type for "a game ready to simulate";
  - there's a latent bug that will appear the moment someone removes a per-step allocation the plan already asks to remove.

## 1. The walk's features array is owned by both the walk and the collector

`GameWalker` allocates a new `double[]` every step and passes it to the decider and the collector. `TrainingDataCollector` stores that array as it is (`new TrainingExample(features, outcome)`). The collector is only correct because the walk allocates a fresh array each time.

`plan.md` lists "the walk allocates no features array per step" as a cost to remove. Whoever does that by reusing a buffer will make every training example point at the same array. The data would be silently wrong: the CSV would be full of the last row's features, and nothing would fail.

The fix is to say in the signatures who owns the buffer:

```csharp
int Decide(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features);
void Collect(NodeSpec<TState> node, TState state, ReadOnlySpan<double> features, int outcome);

// TrainingDataCollector keeps what it needs:
examples.Add(new TrainingExample(features.ToArray(), outcome));
```

After that, the walk can use one buffer sized for the node with the most features, and the compiler won't hit the same trap.

## 2. Incidents are the sport's business, but the engine is generic over them

The engine never uses `TIncidentCollector`; it only passes it to transitions. Even so, it's a type parameter on `NodeSpec`, `GameSpec`, `Next`, `NextNode`, `GameOver`, `Transition`, both engine interfaces and `GameWalker`. The NBA code writes `NodeSpec<NbaState, INbaIncidentCollector>` dozens of times.

One collector is shared by every simulation, which causes a second problem. Every incident has to say which simulation it came from, so `SimulationIndex` lives in `NbaState` and in `PossessionIncident`. The sport's state is carrying simulation bookkeeping. Training-data generation invents an index (`_walks++`) to fill it. The plan for parallel runs (collectors write to slots keyed by `SimulationIndex`) builds on that coupling.

The incident sink should be part of what the sport creates for each simulation:

```csharp
public delegate Next<TState> Transition<TState>(TState state, int outcome);
public sealed class NodeSpec<TState> { ... }

// Simulation: the caller builds each simulation's state, sink included
Simulator.Run(game, simulations, i => new NbaState(schedule, rates, scores.For(i)));

// NBA
public readonly record struct PossessionIncident(int TeamInPossession, int Period, int Points);
```

- One type parameter instead of two throughout the engine.
- `SimulationIndex` disappears from the state and the incident.
- Parallel runs keep the same lock-free property, because each sink writes to its own slot (`scores.For(i)`).
- Training-data generation passes the empty collector, as it does today.
- The trade-off: a feature expression could in principle read `s.Incidents`. That's harmless, and a compiler that reads the expressions can reject it.

## 3. Binding fitted models to a game has no home

Turning a spec plus fitted models into something you can simulate is spread across three places:

- `SimulationModels.For` does the checks and builds a dictionary.
- `SampledOutcomeDecider` looks up `models[node]` in that dictionary at every decision.
- `Simulator.Run` rebuilds all of it on every call.

The consequences:

- Pricing a slate of games with the same fits rebinds every model for every game.
- The hot path pays a dictionary lookup per decision.
- `GameWalker` scans `spec.Nodes.Contains(target)` at every step, because a transition can return any `NodeSpec`, even one from another game.

There should be a type for the bound game, and the walk should run on it:

```csharp
public sealed class GameSpec<TState>
{
    // assigns each node its Index once; a node belongs to one game only
}

public sealed class SimulationGame<TState>
{
    public SimulationGame(GameSpec<TState> spec, IReadOnlyList<FittedModel> fitted) { ... } // today's SimulationModels checks
    public Model ModelOf(NodeSpec<TState> node) => _models[node.Index];
}

Simulator.Run(SimulationGame<TState> game, int simulations, Func<int, TState> createState);
```

- Node indices are the change `plan.md` already plans.
- Giving them to the bound game turns the membership check into `node.Game == this` and the lookup into an array access.
- The bound game is also where the compiler fits in later: a compiled game replaces it, behind the same `Run`, and the digest test compares the two.

## 4. A node's features are stated twice (later, with splines)

`NodeSpec` takes `Feature<TState>[] features` and a `ModelDeclaration` whose terms name features by string. `RequireFeatures` checks at runtime that the two agree.

That works while `NbaNodes.Node` builds the terms from the features. Step 13 brings per-feature bases (a spline on the clock, linear elsewhere), and then the sport writes both lists by hand.

- The alternative: the sport declares terms over `Feature<TState>` objects, and the node derives its feature list from them, so a mismatch can't be represented and the check goes away.
- The cost: a small typed declaration on the sport's side that converts to the contract's `ModelDeclaration`.
- When: when splines come to the engine, not before.

## Leave alone

- `Next`: it's the right model. With node indices it could later become a struct, but it doesn't need to change now.
- Separate decider and collector: they're what let you simulate and still collect decisions for diagnostics.
- `int` outcomes and the `Yes()` checks: typed outcomes are already planned for the first continuous outcome.
- Interface dispatch inside `Model`: the compiler replaces it, and the closed sets keep that possible.

## Order

1. Item 1 first: it's small and stops a silent bug.
2. Items 2 and 3 together next, since both reshape the engine's types and `Simulator.Run`; doing them before the clock work means the clock lands on the final structure.
3. Item 4 waits for splines.
