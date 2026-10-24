# Adding the clock

The clock as built in step 13 (`models.md`).

## What the data says

Measured over the accepted regular-season games.

1. The game clock is whole seconds left in the period (`"11:46"`; `"00:00"` can mean up to a second left). There is no shot clock in the data.
2. Against a shot clock rebuilt by rule, about 3% of actions come late, almost all by 1 to 3 seconds: we time an action from the previous one, while the real shot clock starts a little later (the rebound secured, the inbound touched, the tip controlled). After a made shot 6.0%, a side-out 4.3%, the period start 3.6%, an offensive rebound 2.8%, a defensive rebound 1.7%, a made free throw 1.5%, a turnover 0.6%; when the period clock binds, none.
3. A rebound after a missed shot at 0:00 is bookkeeping (86% recorded as offensive), except in the 2.5% where play follows.
4. Periods 2 to 4 follow the alternating rule in all but 1 game in about 4,000.
5. Free throws outside the tree's plays (technical, flagrant, take-foul, clear-path) are about 1.5 a game, 1.2 points; two games in three have one.

## Hacks it removes

1. Dropping the possessions a period end cuts: they are kept.
2. The real schedule (`PeriodSchedule`, `ObservedGame.Schedule()`): the clock ends periods and the score decides overtime.
3. Ties in the simulation: impossible.
4. The quality-assurance test's check that the number of possessions matches the real game.
5. Ignoring technical, flagrant, take-foul and clear-path free throws: they are free throws outside the play.

Kept, because they are modelling choices or data checks: `InBonus` stays learnt; dead-ball rebounds stay skipped; the rejection of games with more than 4 possessions between the teams stays.

## Rules

1. **The shot clock**, rebuilt by rule: 24 when a possession starts, 14 after an offensive rebound, at least 14 after a side-out (`ShotClock`).
2. **An action's time** is measured from the previous action (or the period start), so every second of a period is counted once. Its bound is `min(shot clock + 4, period clock)`: the 4 seconds of slack cover the gap between the event we time from and the real shot clock's start, and leave 0.3% of observed times above the bound, which are clamped to it (the excess carries into the next action).
3. **A play** starts with `AwardedFreeThrow`, then `PeriodRunsOut` when the period clock is below the shot clock plus 4 seconds, then `ActionType`, then `ActionTime`.
4. **A period ends only through `PeriodRunsOut`**: asked at the start of a play when the period clock binds, and after a miss with 0 on the clock, before the rebound. The possession in progress is reported with its points so far.
5. **Periods** are 720 seconds, overtime 300 (`Periods`). `JumpBall` (did the home team win the tip; no terms) starts the game and every overtime; periods 2 to 4 follow the alternating rule. A tie after the fourth period or an overtime adds another overtime. Team fouls reset each period.
6. **Free throws outside the play**: `AwardedFreeThrow` (none, offence or defence; asked again after each one) and `AwardedFreeThrowMade` (the shooter's margin and scoring rate). They do not move the ball. The reader attaches each to the next play of its period, or the next period's first play; those after a game's last play are counted and dropped.
7. **When the rules and the data disagree** (the rules ask for an action the data does not have, or the reverse), the game's examples are discarded and the game counted: 7 games.

## Features

- `ActionTime` (discrete JSU, given `maximum = Bound`): the chosen action (three, turnover and foul against a two), how the play started (after an offensive rebound, a made shot, a made free throw, a turnover, a side-out or the period start, against a defensive rebound), `Bound` (a spline of 8 knots on location and scale, linear on skew and tail), `LogBound` (skew and tail), `PeriodClockBinds`, `ScoreMargin` (a spline of 6 knots on location and scale, linear on skew and tail), `LateInGame` (the fourth period or later, at most 120 seconds left).
- `ActionType` adds the shot clock and the period clock.
- `PeriodRunsOut`: the period clock, the score margin, the period, and whether it follows a miss.
- `AwardedFreeThrow`: the score margin and `LateInGame`.
- `JumpBall`: none.

## The distribution for `ActionTime`

A discretised truncated JSU (Johnson's SU), chosen by experiment in Python (see `models.md`): a whole number of seconds from 0 up to the bound.

1. **Four predicted parameters**, each with its own terms and link: location (identity), scale (log), skew (identity), tail (log). With Z standard normal, the continuous time is location + scale · sinh((Z − skew) / tail).
2. **Floor edges**: second k is the probability of [k, k + 1), and the distribution is truncated to [0, n + 1).
3. **The bound is a given parameter**, read from a feature, not predicted.
4. **Sampling** inverts the distribution function with one uniform, never outside 0 to the bound, at a fixed cost: the normal distribution function twice (the two ends), its inverse once, and a sinh. .NET has neither, so C# has its own: Cody's rational approximations for erfc and the scaled erfcx (so log Φ stays accurate deep into the lower tail) and Wichura's AS241 for the inverse, pinned against torch by golden files.
