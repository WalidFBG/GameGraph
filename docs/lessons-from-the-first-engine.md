# Lessons from the first engine

Findings from building a simulation engine for an existing NBA simulator. Reused as lessons, not code.

## Design

- Separate the sport's rules from model evaluation and sampling. That split is what made the old engine testable and fast.
- Keep rules as plain C# methods. The engine owns features, models and sampling; turning rules into a mini-language makes them harder to read, not faster.
- Make every optimisation exact, so a digest of the output proves it in seconds. That makes small optimisations cheap to try.
- Give each simulation its own seeded random stream, so results are identical on one core or many.

## Speed (for later)

- Interpreted model evaluation (walking a model's description at run time) was the biggest cost. Compiling each node into one function (features, model and sampling together) fixed it.
- Compile only once the plain version works and is pinned by a test.
- The .NET JIT stops optimising very large methods, and inlining has size limits. Keep generated and hot methods small.
- Formatted exception messages inside hot methods slow them down even when never thrown. Throw from separate helper methods.
- An array lookup beats a switch when the selector changes between calls.
- Calls inside generated code force registers to be saved and restored. Avoid them in the hottest code.

## Measuring

- Compare two versions by alternating them in the same session, never one after the other.
- Check that every DLL in a benchmark is a Release build.
- Measure on one core (pinned, single GC heap) to see the real cost per simulation.
- A sampling profiler cannot see inside generated methods; their time shows up on the caller.
- Profile CPU before cutting allocations.
