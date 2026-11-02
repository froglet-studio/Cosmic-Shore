# Brittle Star Time - Technical Documentation

> **Status.** Authored headless on 2026-10-27. The course and its tests were compiled and RUN outside the editor
> (400 seeds per intensity). **Not run in the Unity editor; nobody has flown it.**

## Overview

`GameModes.BrittleStarTime = 74` is the **Brittle Star's Time card**; it had none. It is Thresher Time's shape: a
lapped circuit of eight rings on Headlong's shared solver, flown for two laps, with the first domain home winning.

## The loop: Speed Mode

- **Speed Mode** (A, `R_VesselActions/BRITTLE_STAR.md` §3.1) runs the hull at six times cruise: 270 u/s (x8 at
  Time 10) against cruise's 45.
- **The cost is the turn.** Its turn barely grows with speed (`0.1 x speed + 80` deg/s), so Speed Mode turns a
  ~145 u circle against cruise's ~30 u.
- **So every corner is one question:** can Speed Mode hold it, or do you drop to Cruise and turn in?

## The ladder (`BrittleStarTimeCourse.ForIntensity`, asserted by `BrittleStarTimeCourseTests`)

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Median corners tighter than Speed Mode's circle (145 u) | 0 | 1 | 2 | 3 |
| Mouth radius | 90 | 68 | 54 | 42 |
| Corner floor | 188 | 108 | 65 | 65 |

Measured over 400 seeds. Every floor sits above 1.5x the cruise circle.

## Reused and new

As for Serpent Time. The AI flies Speed Mode with the hull's shipped `BrittleStarSpeedModeAIPolicySO`, which has
never been tuned in any race. Its approach is sized to Speed Mode's circle (commit 320, lead 360, through 250).
All the assets come from `Tools/Build/author_brittle_star_time_assets.py`.

## Known limitations

- Not run in the editor. The scene needs one open-and-save.
- Card art is the placeholder until `/cardart` has a recipe for it.
- The Brittle Star's prefab has been stable for months. The two old branches that touched it
  (`align-brittlestar-falcon`, `falcon-brittlestar-reintro`) are stale.
