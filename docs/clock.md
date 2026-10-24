# Adding the clock

Decisions so far for step 3 of the plan (the tree's clock). Nothing is built yet.

## What the data says

Measured with a quick script over 349 regular-season games of 2024.

1. The game clock is whole seconds left in the period (`"11:46"`). There is no shot clock in the data.
2. 2.7% of actions come later than a shot clock rebuilt by rule allows, mostly by 1 to 6 seconds after a made basket: the game clock runs during the inbound, the shot clock starts on the touch.
3. Every missed shot just before a period ends is followed by a rebound at 0:00, about 1.8 a game and 86% "offensive"; in 82% of them the shot itself was at 0:00, so the rebound is bookkeeping.
4. 6% of games go to overtime; a jump ball starts each overtime.

## Hacks it removes

1. Dropping the possessions a period end cuts (`PossessionsCutByPeriodEnd`): they are kept.
2. The real schedule (`PeriodSchedule`, `ObservedGame.Schedule()`, `NbaState.Periods` and `RemainingInPeriod`): the clock ends periods and the score decides overtime.
3. Ties in the simulation: impossible.
4. The quality-assurance test's check that the number of possessions matches the real game.

Kept, because they are modelling choices or data checks: `InBonus` stays learnt; take fouls and technicals stay ignored; dead-ball rebounds stay skipped; the rejection of games with more than 4 possessions between the teams stays.

## Decisions

1. An action never takes more than 24 seconds: `ActionTime` is bounded by the rebuilt shot clock or the period clock, whichever is shorter. Observed times beyond the bound (about 2.5%, mostly 1 to 6 seconds after a made basket) are treated as bad data; how the reader handles them is to settle.
2. An action's time is measured from the clock at the previous action (or the period start), so every second of a period is counted once, the time from a shot to its rebound included.
3. The shot clock is rebuilt by rule from the previous action's time: 24 when a possession starts, 14 after an offensive rebound, at least 14 after a side-out. It is not clamped at 0.
4. `PeriodRunsOut` is asked when the period clock is below the rebuilt shot clock. In the data it is yes when the possession has no further action before the period ends. A game where the rules ask for an action the data does not have is rejected under a new reason and counted.
5. A miss that leaves 0 on the period clock has no rebound decision: the period ends. The reader skips the 0:00 rebound after a 0:00 shot; rebounds after a shot at 0:01 or later are kept (about 0.3 a game).
6. `PeriodEnds` reports the possession in progress, with its points so far, if it made any decision. An observed possession may have no actions, or end without an ending action, only when the period ended it. A possession that would start with 0 on the clock does not exist.
7. `JumpBall`, a Bernoulli node (did the home team win the tip, intercept only), starts the game and every overtime. Periods 2 to 4 follow the NBA's alternating rule; games that break it are rejected and counted. The quick script found about 9% breaking it, but it only guessed who won the tip: if the reader confirms 9%, stop and revisit.
8. `Model` allows a model without terms, for `JumpBall`.
9. Regulation periods are 720 seconds, overtime periods 300; a tie after the fourth period or an overtime adds another overtime. Team fouls reset each period.
10. Features, all linear:
    - `ActionTime`: the chosen action (three, turnover and foul indicators against a two), the shot clock, the period clock, the score margin;
    - `ActionType` adds the shot clock and the period clock;
    - `PeriodRunsOut`: the period clock, the score margin, the period;
    - `JumpBall`: none.
11. No new incident types: the possession incident is enough for the score.
12. The quality-assurance test drops the schedule and ties, and prints home and away win, the overtime rate, mean total and mean possessions next to the real game.

## The distribution for `ActionTime`

A discretised truncated JSU (Johnson's SU), chosen by experiment in Python (see `models.md`): a whole number of seconds from 0 up to a bound of at most 24.

1. **Four predicted parameters**, each with its own terms and link: location (identity), scale (log), skew (identity), tail (log). With Z standard normal, the continuous time is location + scale · sinh((Z − skew) / tail).
2. **Floor edges**: second k is the probability of [k, k + 1), and the distribution is truncated to [0, n + 1).
3. **The bound is a given parameter**, read from a feature, not predicted; it replaces a second interface for families with a bound.
4. **Sampling** inverts the distribution function with one uniform, never outside 0 to the bound, at a fixed cost: the normal distribution function twice (the two ends), its inverse once, and a sinh. .NET has neither, so C# needs its own (standard rational approximations) or MathNet: to settle.

## Pieces

1. The `ActionTime` distribution and models without terms, in the new models work (`models.md`): trained in Python, evaluated and sampled in C#.
2. Reading the clock: action times, keeping cut possessions, buzzer rebounds, the period-start rule and its rejections; `ObservedActionsTests` prints time statistics and the new rejection counts.
3. The graph: the clocks in the state, `ClockCheck`, `PeriodRunsOut`, `ActionTime`, `PeriodEnds`, `JumpBall`, overtime; the schedule goes; the observed decider learns the gate.
4. End to end: generate, train, the quality-assurance simulation; then `data.md`, `plan.md` and `possession-tree.html`.
