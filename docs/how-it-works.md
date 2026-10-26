# How it works

## A sport is a graph of decisions

Each node of the graph is one decision with a fitted model behind it: a yes or no (is the shot made?), one of several outcomes (what does the team with the ball do?), a number of seconds (how long did the action take?) or a choice of players (who shoots?). After every decision the sport's rules update the game state (the score, the clocks, who is on court) and say which node comes next, until the game is over. [`graph.html`](graph.html) shows the NBA's graph.

## One walk, two modes

The engine walks the graph the same way in both modes. At every node it computes the node's features from the game state, asks for the outcome, hands the decision to a collector, and applies the node's rule. Only two things differ:

| | Simulate | Generate training data |
|---|---|---|
| The outcome comes from | a draw from the node's fitted model | what happened in a real game |
| Each decision goes to | nowhere | the training data: the node's features and the outcome |

So the features and rules that produce the training data are the very ones that later simulate: what a model was trained on is what it sees when it runs. When a real game does something the rules cannot follow (a player credited while off court, a timeout a team no longer has), that game is rejected, never patched.

## What the outside world sees

A simulation reports **incidents**: the sport's facts as the rules record them, such as a possession and its points, a free throw, a player's credited rebound, a player's minutes. Whatever is priced is built from incidents. Decisions, features and the game state stay inside.

## Game context

Some features describe the teams and players before tip-off: a team's points per possession, a player's shooting rates and shares, over their recent games. They are computed from the games before, taken in start-time order, so a game never sees its own result or a later one.

## C# and Python

The engine, the sport, simulation and training-data generation are C#. Training is Python (PyTorch). They share only files, described by the JSON Schemas in `schemas/`: C# writes each node's training data, Python writes back each node's fitted model and a training report. Parity tests check that C# computes the same likelihoods as Python from the same files, to 1e-12, and draws the same outcomes from the same random numbers.

## Speed

Simulation runs on all cores, one simulation per task, each with its own seeded random numbers, so the results do not depend on how many cores run them. Each node can be compiled into a single method (its features, its model and its draw), which roughly halves the time of a simulation; the plain walk stays for debugging and for training data. The compiled walk gives the same results as the plain one, bit for bit, and a digest test pins the output of 1,000 simulations of a fixed game, so any change in what the simulator does shows up at once.
