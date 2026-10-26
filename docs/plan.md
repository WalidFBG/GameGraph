# Plan

## Goal

A simple, readable prototype that runs the full flow on real NBA data: augment, generate training data, train, simulate, validate. Start almost too simple, then grow one node or one feature at a time, keeping the whole flow working at every step.

## Constraints

- The engine, the sports and simulation are plain .NET 10 and C#, with .NET libraries (for example MathNet) only where they genuinely help. Training is Python (see `models.md`).
- The repository is public: no data is committed, and nothing is copied from other codebases. Findings and lessons are reused; code is written fresh.
- Every line should be understandable on its own. Prefer simple and explicit over clever.

## Step 1: the thinnest full flow

**The game.** Teams alternate possessions. Each possession scores a made field goal (worth 2 points) or not. Each team gets the number of possessions it had in the real game.

**The model.** One additive Bernoulli model for "possession scores a made field goal", with two features:

- the margin in the game's own score (game state, so simulating and generating training data compute it the same way);
- the team's made-field-goal rate per possession over its previous 20 games (game context).

**Reading the data.** How real games become possessions and outcomes, and where created data lives: see `data.md`.

**The flow.**

1. Read real games and walk them into possessions and outcomes.
2. Augment: build each game's context from games strictly before it.
3. Generate training data: walk each game, emit one row per possession.
4. Train the model on the rows.
5. Simulate held-out games; price the total and the winner.
6. Validate against what actually happened: the audit (see "The audit").
7. Round trip: dropped, the audit covers it.

The model is useless for pricing, but every part of the architecture exists and is tested.

## Validation

- **The audit of the simulator and its nodes** (below): it covers what a round trip would (simulation, training-data generation and training agreeing with each other).
- **Against reality**: holdout log-loss and calibration per model; simulated against actual totals, margins and, later, player stats on held-out games.
- **Against the current production model**: a sanity check on averages only. It contains trading adjustments, so it is not the target.

## The audit

Two audits, split between C# and Python. The C# writes data and nothing else: each audit is an explicit xUnit fact in `src/Nba/GameGraph.Nba.Tests.QualityAssurance` (run only when asked for by name, never by a plain `dotnet test`), writing Parquet to `C:\Data\GameGraph\nba\audit\<audit>\<yyMMdd-HHmm>` (written as `<yyMMdd-HHmm>.partial` and renamed when complete, so an analysis never reads a run that failed or is still being written); the project keeps its entry points (tests and audits) at its root, readers and writers in `Infrastructure`, the data written in `Definitions` and the logic in `Domain`. Python (`python/src/gamegraph/audit`) reads it, groups it and writes CSVs named `<node>_<feature>_<run>.csv` (so that two runs open side by side in Excel), with a summary per node; nothing passes or fails. Nothing names a node or a feature: those come from the training data's manifests and the fitted models, so a new node is audited as soon as it exists.

1. **Examples**: both audits work on each node's examples (features, outcome), every game at once, with no split. Real ones are read from the training-data files, so the audit sees exactly the rows the trainer saw; simulated ones come from the simulator through the same `RecordingDecisionCollector` that records the training data.
2. **Buckets** (Python): a feature with at most 30 distinct values gets one bucket per value, any other up to 20 quantile buckets, never splitting equal values. A z-score per bucket and outcome uses the variance of a sum of independent Bernoullis, and is left out when fewer than 5 outcomes are expected; the summary sets the number of z-scores above 3 against the number chance alone gives, lists the worst, and `failures_<run>.csv` holds them all.
3. **The calibration audit**: does each fitted model follow its features? The C# writes every example with the C# model's probability of each outcome (`p0` to `pK−1`; for `ActionTime`, outcomes past a row's bound have probability 0), so that the translation from Python is audited too. Per node, feature and bucket, Python compares the observed shares with the predicted ones; `ActionTime` compares its mean and quartiles.
4. **The simulation audit**: does the simulator behave like the data? Every generated game is simulated once, with its own seed drawn from the root seed in game order, so runs repeat exactly: more simulations per game would only narrow the simulated side's noise, which is no larger than the real side's. Only the teams and each game's context (from the history of earlier games) come from the real game; every decision is sampled.
   - Node analysis: per node, and per feature bucket cut on the real data, how often the node is reached per game (a z-score on the per-game differences, since both sides are the same games) and its outcome shares, simulated against real. A node reached too often or too rarely points at the transitions or the state, not at the model.
   - Sampling: the calibration analysis run on the simulated examples, per node, feature and bucket, the simulated outcomes against the model's predictions, which match by construction unless sampling is wrong.
   - Real games are counted the way simulations are, by walking them through the graph, so points, possessions and periods mean the same on both sides (the data's possessions miss the empty possession a period's end cuts off).
   - Game statistics: questions we choose, a short list at the top of the module: points, totals, the home margin, possessions and overtime from each game's incidents, and attempts, makes, free throws, fouls, rebounds and turnovers counted from the decisions; with the distributions of totals and margins.

## Augmentation

Augmentation runs side by side with training-data generation (and later the simulator), never as a separate file. Games are walked in start-time order; for each game, the context is read from the history of earlier games, training data is generated from the game or it is simulated with it, and only then is the game added to the history.

1. No looking ahead: guaranteed by the order of the walk, and a test checks it.
2. One code path for training and live pricing.
3. Small samples (rookies, trades, returns from injury) need explicit shrinkage or weighting, never hidden fixes.
4. Context enters the engine as constant per-game inputs next to the game state.

## Simulation (step 5)

Built in two pieces: first the engine change (decision collector and incident collector, incidents reported by the NBA's transitions), then simulation itself.

1. **Parameters**: `GameGraph.Parameters` is the contract between training and simulation (the family, and per predictor an intercept and each term's coefficients, and the file format). Callers read the files; simulation rebuilds the model from them and fails if a term's feature is not one of the node's features.
2. **Randomness**: one root seed constant; each simulation gets its own seed, drawn from the root up front, so results do not depend on scheduling. `System.Random(seed)` sits behind a small class of our own, to be replaced by xoshiro256** (seeded through SplitMix64) with the speed work, before outputs are pinned by a digest test.
3. **Count**: 10,000 simulations per game.
4. **The simulation index**: each incident carries the index of its simulation (`SimulationIndex`), read from the game state, so one incident collector receives all simulations. Superseded: each simulation's state now holds its own incident collector, so incidents carry no index.
5. **The real schedule**: each simulation plays the real game's periods and possessions per period, overtime included; every possession's outcome is sampled. This leaks the real game into the price (overtime is known in advance, so is the pace) and is accepted until step 4 brings the clock; ties are possible and counted separately.
6. **Quality assurance**: analyses are tests in `src/Nba/GameGraph.Nba.Tests.QualityAssurance` (xUnit v3), sharing the season loop (`GeneratedGames`) with training-data generation. The first simulates the latest generated game, writes one row per simulation (simulation index, home points, away points) to `quality-assurance`, and prints home win, away win, tie and mean total next to the actual result. These analyses were later replaced by the audits below and removed.

## Growing the NBA

The next model is the team-level possession tree in `possession-tree.html`: every node's model family, outcome type, transitions, clock rules and incidents.

1. Step 1 above (team level, one Bernoulli node): done.
2. The tree without the clock, on the real schedule: action types, shots, fouls, free throws and rebounds (the Categorical family; rules with their own state). Whether a non-shooting foul gives free throws is a Bernoulli node (`InBonus`) rather than the bonus rule, because the tree does not generate every team foul. Fouls come now because about one possession in eight has free throws: most are a fouled missed shot, which the data records as a shooting foul and free throws, with no shot.
3. The tree's clock (step 13 in `models.md`): done. `PeriodRunsOut` ends periods, `JumpBall` starts the game and every overtime, `ActionTime` is a discrete Johnson SU, and free throws outside the play keep the score right; the schedule leak is gone, and possessions the period cuts short are kept (`PossessionsCutByPeriodEnd` only counts them now).
4. The audit (see "The audit"): first, so that every later step is judged by it.
5. The team graph's gaps, one at a time:
   - the context is points per possession: done. It used to be the share of possessions with a made field goal, so a three counted as a two and free throws not at all; points per possession validates better on five of the six nodes that read it (`AwardedFreeThrowMade` by 0.00007) and worse on `ActionType` by 0.00002;
   - the ball after free throws outside the play: done. `AwardedFreeThrow` learns each free throw's kind (technical, flagrant, clear path, take foul) and remembers the play's previous one, and the ball moves by rule (`AwardedBall`) after a flagrant or clear-path free throw to the defence: about 0.011 times a game, and the 52 games this used to reject are kept;
   - late-game fouling: done. A trailing defence fouls on about 80% of actions in the last 30 seconds when down 1 to 8, which `ActionType` predicted at 7.5%; a few late margin bands, each with an indicator and a spline over the seconds left, now give it, and the simulated table matches the real one. `InBonus` follows the penalty rule (5 fouls, 4 in overtime, or the second in a period's last two minutes; validation 0.122 to 0.031) and `PeriodRunsOut` gets its own bands (0.430 to 0.310). Overtimes went from 2.7% to 3.5% of simulated games, against 4.8% real; the rest of that gap is still open, and the largest miscalibration left in `ActionType` is the threes in every period's last 37 seconds (0.306 predicted against 0.341);
   - timeouts: done. `OffenceTimeout` and `DefenceTimeout` are asked before every play and free throw while the team has one left (7 a game, the fourth period's limits, 2 per overtime), so substitutions can later use them as moments (21.6% of lineup changes are at one). The mandatory timeouts are learnt from a spline over the clock for each count of the period's timeouts so far. Simulated, 10.6 timeouts a game against 10.8 real, and the play after a late timeout is faster (10.0 seconds after a made shot against 13.4; real 8.0 against 13.0). The team graph is complete.
   - dead and live balls: done. Two nodes split the play starts: `Stolen` after a turnover (56% are steals, the rest dead balls) and `TeamRebound` after a rebound (a team rebound is the ball out of bounds: 20% of offensive rebounds, 6.5% of defensive ones). How the play started gains a steal, a dead-ball turnover and a team rebound at either end. `ActionTime` gains 0.039 in validation a visit (the next action comes 6.5 seconds after a steal, 13.4 after a dead-ball turnover), `ActionType` 0.015 (after a team rebound only 51 to 61% of actions are shots, against 75%), `OffenceTimeout` 0.0015, `DefenceTimeout` 0.0007. Simulated possessions went from 199.8 to 200.2 a game (real 200.3) and overtimes from 2.7% to 3.0%;
   - lean nodes: every feature or block stays only if dropping it costs at least 0.0001 in validation per visit. The four nodes that had grown crossed 0/1 cells now have spline-per-band features instead: `OffenceTimeout` 117 features to 20, `DefenceTimeout` 117 to 17, `ActionType` 82 to 15, `PeriodRunsOut` 57 to 17; validation is worse by 0.0006 to 0.0023 a visit.
6. Players and substitutions: who shoots, rebounds, turns it over and is fouled (the Choice family), and the five on court come from the simulation, not the real game.

   **The player layer is a one-way consumer of the team graph.** Team state (margin, clock, period, fouls, timeouts, stoppages) feeds the player and substitution nodes; who is on court, who shoots, who fouls and who is fouled change nothing in a team node, so the team graph prices exactly as before. Players still affect players (a player's fouls drive foul trouble and foul-outs, his minutes fatigue). How the five on court changes the team nodes (their strength as team context, and the shooter's own rates) waits for slice 4 below. The cost falls on player markets: `FouledOnShot`, `ShotMade` and `FreeThrowMade` never know the shooter, so every player converts at his team's rate for the situation (a 38% and a 62% shooter of the same team's twos alike, a 90% and a 55% free-throw shooter alike), and a player's points are his share times his team's efficiency: calibrated in total, biased for each player. The shooter-dependent rates (fouled, made, free throws) are the first candidates to lift the rule for at the tuning; `Shooter` already comes before them, so the order allows it. All in one graph and one walk: the player nodes need the live state, but never write what a team node reads.

   One slice at a time, each its own pull request:
   1. the Choice family and the lineup state, with the first player node, `Shooter`, end to end:
      - **Choice**: one feature row per candidate, P(candidate) a softmax of each candidate's utility; the utility has no intercept, since it would cancel, and the distribution fixes that as it fixes links. Training data is long: one row per candidate, a `decision` column, and `outcome` 1 on the chosen row; an example is a decision.
      - **The engine**: a node is a vector node (one row of features of the state) or a choice node (candidates from an expression over the state, and features of the state and a candidate, one row per candidate); the outcome is the chosen row.
      - **The lineup state**: players are slots within the game (home roster, then away); `OnCourt` holds each team's five. Until substitutions are simulated, it is the real game's five at the action's period and clock, before the lineup changes made in that second (except at a period's full clock, which come before its first action): 29 of 776,235 shooters fall outside it. A simulated overtime the real game did not have reads the real game's last period. This leaks the real rotation, and touches only who is credited, never the team price.
      - **`Shooter`**: after `ActionTime` for a two or a three, before `FouledOnShot`; the team still chooses the action type and the player is chosen given it. Its feature is the log of the player's share of his team's attempts of that type while he was on court over his last 20 games, shrunk towards 1/5 (`PlayerHistory`). A shooter who is not among the five rejects the game (`RulesDisagreeWithData`).
   2. substitutions, modelled as in `Quants.Models.Basketball` (its findings, not its code), in two pull requests:
      - **Sets in the Choice family**: done. Choice picks an unordered set of k candidates, P(S) = exp(Σ utilities in S) / e_k, with k a given feature on every row. The outcome is a bitmask of the chosen rows, and decisions may have different numbers of candidates. `Shooter` is the k = 1 case and keeps its fit.
      - **The substitutions themselves**: done. `Starters` picks each team's five from its active roster before the jump ball (start share, usual seconds, whether he started his last game; it picks 4.2 of the real five a team). At every play and free throw, after timeouts, `Substitutions` (none, the offence, the defence or both; validation 0.3174) asks who changes, then for each changing team, offence first, `SubstitutionCount` (a choice of 1 to min(5, bench)), `SubstitutedOff` and `SubstitutedOn` (time since his last change as a 16-knot spline of its log, seconds played against his usual pace, his usual seconds, whether he started, and at a period's start). The five on court is state in both modes; the replay attaches each net change to the next moment in event order (17 more games are read; two are lost: a buzzer three by a player the data has already taken off, and a change after the game's last moment). Simulated, a team substitutes as often as real (283 moments, 39 changes a game); when a player's simulated start matches the real one, starters play 1,774 seconds against 1,856 and the bench 681 against 640, and a wrong start moves about ten minutes. The audit writes each player's seconds, real and simulated. Measured on the data's 172,936 lineup changes. Each change is attached, in event order, to the next moment the graph asks:
        - about 121,000 land before a play: after a made shot then a timeout, a period's start, a side-out, a turnover (almost all dead-ball ones), a rebound (almost all a team rebound: a missed first free throw or the ball out of bounds);
        - about 50,000 land before a free throw (38,093 between free throws);
        - about 500 come after a period's last action, and join the next period's start;
        - in event order, only 8 of 776,235 shooters are off court, against 29 by the clock.
      - **when**: one opportunity per team at every play start and every free throw, after that moment's timeouts. Substitutions need a dead ball, but no rule asks only then: at a live start (a player's rebound, a steal, a made shot) a team still changes at 0.3 to 0.6% of moments without a timeout, almost all after a dead ball inside the play that the graph does not model (the ball out of bounds off a deflection, a kicked ball): 4,658 changes, about one a game, which a rule would leave nowhere to go. The kind of start and whether a timeout was just called are features instead: at dead starts a team changes at 12 to 60% of moments, at live ones under 1.1%, and after a timeout at 40 to 52% whatever the start. The outcome is the net change over the moment: 183 moments net to nothing, and 920 hold several change events.
      - **the nodes**, per team at each moment:
        - `Substitutes` (Bernoulli): is there a change?
        - `SubstitutionCount`: a choice among the counts from 1 to what the bench allows. A team has 8 to 17 active players, so a categorical from 0 to 5 could draw more than its bench.
        - `SubstitutedOff` (k of the five) and `SubstitutedOn` (k of the bench), independent given the count. Each is skipped when there is no choice.
      - **the bench**: the game's active players (played, a coach's decision, or active but unused) not on court. Today they come from the real game, and in production from the injury report.
      - **starters**: a choice of five from the active players.
      - **features**, each kept only if it earns its place:
        - for the moment: its kind and whether a timeout was just called, between free throws, the period, the clock and the late margin, and the five's longest stint;
        - for each candidate: the stint or rest so far, his minutes against his usual, his usual minutes and whether he is a starter (`PlayerHistory` gains minutes and starts);
      - **foul trouble and the forced foul-out** (845 players reached six fouls, all taken off) wait for slice 3, which simulates who fouls.
   3. the other player nodes, in four pull requests:
      - **who rebounds, turns it over, fouls and is fouled**: done. Five nodes, each a choice of one from a five, with the player's share of his team's events of that kind while on court over his last 20 games, shrunk towards 1/5 (`PlayerHistory`): `Rebounder` after a player's rebound (and whether he missed the shot himself), `TurnoverKind` (a team's: 5% of turnovers name no player, almost all shot-clock violations; an offensive foul: 11%, the player's foul too; another player's, then `Stolen`) then `TurnedOverBy`, `Fouler` after every foul the graph has (his fouls so far, and foul trouble), and `FouledPlayer` after a non-shooting foul (on a shooting foul it is the shooter). A lineup change logged between a missed last free throw and its rebound now joins that free throw's moment. Simulated, a player's rebounds (3.36 against 3.35), turnovers (1.02 against 1.02) and fouls (1.44 against 1.45) match in total; the audit writes them per player.
      - **foul trouble and the forced foul-out**: done. `SubstitutedOff` and `SubstitutedOn` read a player's fouls and whether he is in foul trouble (his fouls as a spline over 0 to 6; validation 1.5523 to 1.5039, and 1.9926 to 1.9769). Before a play with time left, a player with six fouls on court is forced off and never comes back; his team must change, `SubstitutionCount` starts at the number forced off, and `SubstitutedOff` chooses only the rest. At a free throw he is not forced: the data often logs his replacement after the free throws. `Substitutions` knows whether each team must change and how many of the defence's five are in foul trouble (0.3174 to 0.3152). Simulated, a player in foul trouble is among the fouler's candidates 3.4 times a game against 2.3 real (6.0 before). Six games break the rule in the data (a player who never leaves, or who comes back on) and are rejected. Simulated, 0.52% to 0.67% of players foul out over single runs, against 0.69% real. Only the fouls the graph has are counted (99% of personal fouls, offensive fouls included), so a count never runs above the real one, and 920 of the 965 real foul-outs reach six.
      - **steals, assists and blocks**: done. `Stealer` after a steal; `Assisted` then `Assister` (one of the other four) after a made shot; `Blocked` then `Blocker` after a missed shot; each player choice with one share feature; `Assisted` and `Blocked` read the shooter's own assisted and blocked rates (shrunk towards 0.63 and 0.055 by 10 shots). The player audit adds each player's points, attempts, threes, free throws, assists, steals and blocks: in total they match real (points 8.71 against 8.74, assists 2.02 against 2.03, steals 0.59 against 0.60), except blocks, 5% high (0.393 against 0.375): big men miss more often simulated than real, because `ShotMade` does not know the shooter, which slice 4 takes up.
      - **awarded free throws and the remaining fouls**: done. `AwardedShooter` picks who takes each awarded free throw, from everyone on the team's court in that dead ball (the graph asks the dead ball's substitutions first, but about 5% of awarded shooters leave right after their free throw), by his share of the team's awarded free throws, whether he took the previous one and whether he is on court now. `AwardedFouler` picks the take foul's fouler, who gains the foul. A flagrant's fouler is not credited: its free throws point at the foul it upgraded, often one the tree already counts. A player's technical fouls only matter for an ejection at two, and stay out.
   4. players affect the team: the rule above ends here, and only through features that earn their place.
      - **the investigation** first: candidate features per team node from the five on court and from the player deciding (the five's shooting quality for `ShotMade`, their rebounding share for `OffensiveRebound`, their turnover rate for `TurnoverKind` and `ActionType`, the shooter's own rates for `ShotMade`, `FouledOnShot` and `FreeThrowMade`, with make rates as new shrunk history), each measured as usual (kept only if dropping it costs at least 0.0001 in validation per visit). The result is a short list written here before any change.
      - **then one or two pull requests** for what survives: the shooter's own rates and the strength of the five touch different nodes, so they may be two.

   The data's lineups are clean: every team has one starting lineup and no change ever takes off a player who is not on, puts on one who is, or leaves other than five. Per-player history brings small samples (rookies, trades, returns from injury), which need explicit shrinkage (see "Augmentation").

   Two reshapes come with players, because they bring the uses that shape them:
   - **Perspective**: whose decision it is. `ShooterMargin`, `ShooterPointsPerPossession` and `ShooterAtHome` copy the team features for the shooting team; players add the shooter, the candidate rebounder and the fouled player. One feature set is built for a side given as an expression, and the side's body is substituted into each feature, so the result is still an expression a compiler can read, never a delegate.
   - **The observed cursor**: `ObservedOutcomeDecider` flattens each possession into the graph's decision order, so the order is written twice and the two can disagree only at runtime (`RulesDisagreeWithData`). Instead, each node reads its own fact from a cursor over the observed game (`ActionType` moves to the next action, `ShotMade` reads that action and fails if it is not a shot), and the walk still ends with everything consumed.
7. Compilation (see below), once players have brought their family, the feature row per candidate and the lineup state: a compiler built before them would be rewritten for them.

Each step adds its nodes, reshapes the simulator where they need it, adds a family if they need one, and then goes through training data, training, simulation and the audit.

Until compilation, two habits keep it cheap: rules stay static methods, and the game state stays plain arrays and numbers.

Once the graph is complete, and not before, comes the tuning: richer context (the opponent's defence, pace, a rate per node rather than points per possession for all, rest and travel; with players, much of it becomes the strength of the five on court; then `GameContext` moves from Augmentation into `GameGraph.Nba` as the state's constructor input, copied into plain arrays), the drift (idea 7 in `models.md`), and whether games outside the regular season feed the history or the training data.

### Building step 2, piece by piece

Each piece builds and runs on its own and is reviewed before the next.

1. Models as GLMs: terms, linear predictors and families (`Bernoulli`, `Categorical`) working on η; the trainer, the parameters file and the simulator go through the model: done.
2. Each node declares its model (family and terms); the manifest records it; the trainer and the simulator build the model from it: done.
3. Features as expressions (`Expression<Func<TState, double>>`, compiled once for the plain walk), while there are only two features: done.
4. Reading the data: an observed possession becomes a sequence of actions (two, three, turnover, non-shooting foul), with fouled or not, made or missed, the free throws and the offensive rebound.
5. The NBA graph: the new nodes, state, features, transitions and incidents: done (the six decisions of the tree without the clock; first features are the score margin and the team's points per possession plus each node's own state; the possession incident is the only incident yet).
6. End to end: training data, training and the quality-assurance simulation on the new graph: done. `InBonus` stops at the 200-step cap and `FouledOnShot` close to it, so plain gradient descent is already too slow for some nodes (see the optimiser question).

## Model families and data shapes

What the C# models are today; items 1 to 4 are built. `models.md` replaces this: GAMLSS, trained in Python, evaluated and sampled in C#.

1. **Models are GLMs**: terms (a feature and its basis) expand into design columns; each linear predictor is an intercept plus a coefficient per column; the family (link and distribution) works only on η. Every coefficient belongs to one term of one predictor, so nothing has to be inferred from a layout.
2. **A model-family interface** (`IModelFamily`): the number of predictors, the negative log-likelihood, its gradient with respect to η, and sampling from one uniform number. The gradient with respect to the coefficients is the chain rule, done once by the model.
3. **Scaling works on design columns**: training standardises each column and converts the coefficients back afterwards, the same way for every basis. A family's own parameters (a count's dispersion) will sit outside the predictors and are not rescaled.
4. **Each node declares its model** (family and terms) in the sport's spec; the manifest records it and training and simulation build the same model from it.
5. **Outcomes stay `int` until the first `double`**: Bernoulli (0 or 1), categories and counts are all whole numbers, and each family rejects outcomes outside its range, loudly. Typed outcomes (`bool`, `int`, `double`) arrive with the first continuous outcome, when integers can no longer hold it: one node class per outcome type under a common base, and one decide and one collect method per type, so nothing is cast or boxed. Training data keeps 0/1 until then.
6. **Two feature shapes, not a generic one**:
   - vector: one feature set per decision; flat `double[]` of n × f plus a typed outcome array;
   - choice: one feature set per candidate (who shoots); flat `double[]` of all candidates × f, an offset per decision and the chosen rows as a bitmask (long format, one CSV row per candidate).
7. **Splines as a second basis**: `SplineTerm(feature, knots)` expands a feature into `x, max(0, x − k₁), …` columns, and training data keeps raw features. Families, predictors and scaling do not change; the parameters file records each spline term's knots next to its coefficients.
8. **Speed without our own codegen**: for readability, `Model` holds its family behind `IModelFamily` for now, so the family's arithmetic is an interface call and cannot be inlined. Simulation loses nothing by it, because compilation writes each node's family arithmetic out directly. Training is not compiled: its speed work makes the trainer generic over the family's struct again (`where TFamily : struct, IModelFamily`, one dispatch from the family's name to its struct, in the trainer only), together with spans of flat arrays, no allocation per example and SIMD (`System.Numerics.Tensors`).

## Compilation

- **Training does not walk the graph**: it is Python, reading the training-data files, so it is independent of the engine and its speed comes from PyTorch.
- **Simulation**: a compiled node fuses its features, predictor, family sampling and transition into one method, with its parameters as constants; the plain walk stays as the reference, and a digest test proves the two identical.
- **What keeps it possible**: rules are static methods, the state is plain arrays and numbers, families are pure functions of parameters, features and one uniform number, and features are expressions over the state (no calls into arbitrary methods) so a compiler can read them.
- **Bases stay a closed set** (linear, spline): a compiler generates each basis's arithmetic directly, so no term may hide its logic behind an arbitrary `ITerm` implementation.
- **Plain-walk costs to remove on the way**:
  - nodes become integer indices into the game's node array, so the walk and the sampling decider find nodes and parameters by index: no dictionary by node, and no search of the node list at every step (an index outside the array still fails loudly): done, through `FittedGame`;
  - the walk allocates no features array per step: done;
  - `Categorical` allocates no small arrays per call: done, it works on spans and `Model` puts the parameters on the stack.
- **Parallel simulations**: each simulation already has its own seed, so simulations run in parallel, one walk at a time per thread. Each simulation's state holds its own incident collector, writing into its own preallocated slot, so they need no locks. A test checks that the results do not depend on the number of threads.

## Open questions

- Whether games outside the regular season should feed the history or the training data: only regular-season games are read, for both (settled once the graph is complete).
- Licence for the public repository.
