# Restructure of the sport-agnostic code

What to change in the sport-agnostic C# (Engine, Simulation, Models, Contract, Contract.Files, Host) and Python (`gamegraph`) so they read top to bottom, without giving anything up.

## The bar

Every change keeps all of these:

- the pinned simulation digest, unchanged;
- the compiled walk equal to the plain walk bit for bit, with the same hash on one thread or many;
- regenerated training data byte-identical;
- the benchmark median within noise, measured in runs interleaved with `main` (the machine moves between a fast and a slow state).

Items 2 and 7 touch the hot path. The struct steps exist so the JIT can inline them, and `node.Index` is what makes a compiled decision an array lookup.

## How it lands

One PR, in commits that can be reviewed one at a time: the renames and moves first, then each structural change. The file formats don't change.

## Design first

These need their types agreed before any code:

- **The candidate type** (item 1): settled, and done. `ChoiceShape`, a `readonly record struct` in `GameGraph.Contract.Distributions`, is a choice of *k* of *n* rows. It owns:
  - the limit of 31;
  - building from the size feature;
  - `HasOneOutcome`;
  - checking an outcome;
  - the masks.

  The outcome stays an `int` in the walk. The feature layout and `ChoosesAmongCandidates()` stay where they were. Python's `ChoiceShape` (P2) is the batched mirror.

  A recorded choice still may choose every candidate: the Python-generated parity data has 106 of 2,000 such decisions, so only a choice of none is rejected.
- **The node index** (item 7): settled, and done.
  - Every node is created through a `GraphBuilder` for one graph, which hands out its index. `Build(start)` makes the `Graph`, and creating a node after that throws.
  - `Contains` is a reference comparison.
  - The node order is the declaration order in `NbaNodes` (`FouledOnShot` moved up to keep the indices). `NbaGraph` no longer lists the nodes.
- **The names in item 8**: settled, and done.
  - `NodeSpec` is now `Node`, `GameSpec` is now `Graph`, and `VectorNode` is now `SingleRowNode`.
  - `FittedGame` and `CompiledGame` are now `FittedGraph` and `CompiledGraph`, and `NbaGameSpec` is now `NbaGraph`.
  - `ContractFiles` and `TrainingDataFiles` are now `ContractDocuments` and `TrainingDataDirectory` (Python: `contract_documents`, `training_data_directory`).
  - `FeatureTerm` and `CandidateTerm` stay two types: a generic one would need an interface and longer signatures to save about 20 lines.

## C#

1. **One type for "candidate rows and a chosen set".** A choice's features are one row per candidate and its outcome is a bitmask of the chosen rows. That idea is now re-derived in `ChoiceNode` (writing features, `Apply`, `TryRowsOf`), `CompiledGame.ChoiceParameters`, `Model.Rows` (with its `_choosesAmongCandidates` flag), `Choice.Decision` and `TrainingDataFiles` (`WriteChoice`, `Choices`, `Chosen`).
   - The type exists in memory only.
   - It absorbs the 31-candidate limit, now written in three places, and the question "is there only one outcome?" (a size of 0, or every candidate).
   - `TrainingData` keeps checking the shape at the boundary, by parsing into the new type.
   - `VectorNode` only rejects choice distributions, so it stays as it is.
2. **Name the walk's types; don't merge them.** There are two collector interfaces because the compiled walk has no features to pass on.
   - The interface names say which one gets features and which gets only outcomes.
   - The do-nothing collectors share one naming pattern (now `EmptyDecisionCollector` and `NoOutcomeCollector`).
   - The walk's types go in one folder (item 10).
   - Cutting the number of types further would cost either the struct dispatch or the training data's interface.
3. **Remove the reflection from `CompiledGame`.**
   - Each project gives out an expression of its own call, such as `model.SampleExpression(parameters, count, uniform)` and `choice.RequireSizeExpression(size, count)`.
   - That removes the `GetMethod` calls and Models' `InternalsVisibleTo` for Simulation, and `RequireSize` no longer needs to be public.
   - Each piece of compile code stays beside its interpreted twin (`Predictor.ValueExpression` next to `Predictor.Value`); that is how the summation order stays bit-identical.
4. **Take the test-only `CompiledGame.ParametersOf` off the production class.**
5. **Move `GameGraph.Host` under `src/Nba`.** It is the NBA training-data generator, with NBA paths. It becomes, for example, the entry point of `GameGraph.Nba.TrainingDataGeneration`.
6. **Make the recorder's call order a matter of types.**
   - `BeginGame` returns a recorder for that one game, instead of the first decision throwing when it wasn't called.
   - "A decision with one outcome isn't recorded" stays the recorder's rule. It asks item 1's type, so the virtual `NodeSpec.HasOneOutcome` goes.
7. **`GameSpec` changes its nodes** (design first). `JoinGame` sets each node's index, so a node carries a nullable `_index`, `IsInGame` and an `Index` that throws, and can belong to only one game.
   - A dictionary lookup would cost speed.
   - The NBA nodes being static fields that point at each other is no blocker, because transitions read those fields only when they run.
8. **Renames:**
   - `NodeSpec` and `GameSpec`: "Spec" adds nothing.
   - `VectorNode`: vague; it means one row of features, against `ChoiceNode`'s row per candidate.
   - `FeatureTerm` and `CandidateTerm`: identical apart from the feature type, so they become one generic type.
   - `ContractFiles` and `TrainingDataFiles`: the names don't tell the JSON documents from the training-data folder.
9. **Give `ReadTrainingReport` a typed record** in place of a `JsonObject`, as the Python already has. It stays in Contract.Files, behind the schema check.
10. **Folders:**
    - Engine: a `Graph/` folder (nodes, features, terms, transitions, `Next`) and a `Walk/` folder (the walker, steps, deciders, collectors).
    - Simulation: random numbers (`SeededRandom`, `SplitMix64`, `Seeds`) apart from the plain and compiled paths.
    - Each transition delegate moves beside its node.

## Python

- **P1. `audit/simulation.py` is NBA code** in the sport-agnostic package. Its `COUNTS` name NBA nodes and outcomes (`ActionType`, `ShotMade`, `FreeThrowMade`). `PLAYER_COUNTS`, `FOUL_OUT = 6`, `REGULATION_PERIODS = 4` and the home and away points are NBA facts too. Split it:
  - the per-node comparison of real and simulated decisions stays sport-agnostic;
  - the game and player statistics move to an NBA module, which stays out of the trainer's digest like the rest of `audit`.
- **P2. A choice is handled in five places,** through `chooses_among_candidates()`:
  - `training_data_files` (reading and writing)
  - `Model.designs`
  - `Model.initial`
  - `TrainingData`
  - `audit/simulation._real`

  Here a choice is a padded tensor of decisions × candidates × features, and a size of 0 marks a padding row. Give it a type that mirrors item 1 and holds the candidate mask explicitly.
- **P3. `contract.py` has 509 lines.** It holds the distributions, terms, model declaration, fitted model, training-data manifest, training report and UTC text in one module. Split it into a `contract` package with one module per document. The imports stay `from gamegraph.contract import …`.
- **P4. Two classes are both called `Failure`:** the training report's failed attempt (`contract.Failure`) and the audit's finding (`audit.findings.Failure`). Rename both, for example `FailedAttempt` and `Finding`.
- **P5. `files` depends on `training`.** `training_data_files` builds `training.training_data.TrainingData`, a tensor type with the same name as C#'s contract record. Put the tensor types where both `files` and `training` can depend on them, under a name that says they are tensors.
- **P6. `Model`'s `…_of` methods** (`parameter_values_of`, `log_prob_of`) take designs, and the plain ones take features; the suffix doesn't say so. Name them for what they take.
- **P7. `buckets.outcome_prefixes` returns `None`** to mean "compare means instead". Return what to compare, or split the function.

## Kept, against the plan

- **`Node.HasOneOutcome` stays virtual** (item 6). The recorder sees only a `Node`, so removing it would need a type check for `ChoiceNode`. A choice node answers through `ChoiceShape`.
- **`ReadTrainingReport` stays a validated `JsonObject`** (item 9). Only the parity test reads it, for one field, and a typed record would add a dozen fields with no C# reader.
- **The two collector interfaces keep their names** (item 2). `IDecisionCollector` is given each decision (its features and outcome) and `IOutcomeCollector` only outcomes, so the names already say which is which. The do-nothing collectors are now `NoDecisionCollector` and `NoOutcomeCollector`.

## Kept as it is

- **The size in every candidate row.** The choice model reads it as a given parameter per row, and the Python uses a size of 0 to mark padding rows. Removing it would change `Model`'s parameter layout, the file format and the Python, for readability alone.
- **`LogOnePlus` and `MathMethods`.** `LogOnePlus` is the function's real name (log1p), and `MathMethods` holds the `Math.Min` and `Math.Max` the compiler calls.
- **Test projects where they are.** The NBA tests need the Fixture project, which is a reason for them to stay beside the NBA code.
- **The small files** (`Next`, `Link`, `Utc`, `DistinctNames`): normal for C#.
- **Contract:** records that check themselves when built.
- **Models:** the distributions, `Predictor` and `KnotLookup` read well.
- **Class sizes:** none is too large. The biggest are `Normal` (190 lines, mostly constants) and `Choice` (195).
