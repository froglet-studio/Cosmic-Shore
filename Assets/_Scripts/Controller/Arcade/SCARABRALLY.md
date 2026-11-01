# Scarab Rally - Technical Documentation

> **Status.** Authored headless on 2026-10-27 (`cece/confident-goodall-6cpxan`). The course and its tests were compiled and
> RUN outside the editor (400 seeds per intensity, against the card-art harness's Unity shim). **Nothing here has
> been run in the Unity editor**, and nobody has flown it.

## Overview

`GameModes.ScarabRally = 72` is the **Scarab's Time card**: a ring race with no ball. Garrett, 2026-10-27:
"a proper ring race for scarab that doesn't rely on the ball at all but has the scarab racing around the map using
the throttle drift and juke mechanics similar to sparrows game." Scarab Scramble stays the Scarab's ball game and
is now its **Mass** card (`ModeGenre`).

An open chain of 24 switch rings is walked through the barren race cell, every pilot flies it in order, and the
first DOMAIN whose lead runner threads the last ring wins (golf: finish time).

## The loop, in the Scarab's own terms

| Verb | What it is (`SCARAB.md` §3) | What the course asks of it |
|---|---|---|
| **Throttle** (RT) | An integrator: +90 u/s² along the nose up to 216 u/s (x1.5 at Time 10). Released, it sheds 120 u/s every second | Hold it. A lift costs speed, and winding back up takes 2.4 s. Every leg is longer than a wind-up from rest (259 u), so a lift costs time, never the next ring |
| **Drift** (LT) | Decouples the course from the nose and keeps 100% of the speed | Hairpins (90-150°) are far tighter than the ~102 u full-throttle circle. You either lift or drift |
| **Juke** (right stick to the rim) | A ~40 u sideways hop (80 u/s for 0.5 s), uncooled | A mouth twisted off your line is a juke, not a correction. The tightest mouth (22 u) is wider than half a juke, so one juke always puts you in it |

## The ladder (`ScarabRallyCourse.ForIntensity`, asserted by `ScarabRallyCourseTests`)

| | I1: learn the throttle | I2: drift | I3: the juke | I4: gauntlet |
|---|---|---|---|---|
| Mouth radius | 64 | 46 | 32 | 22 |
| Hairpins | none | every 2nd corner (90-150°) | every 3rd (90-130°) | every 2nd (110-150°) |
| Mouth twist (jitter / cap) | 15° / 40° | 25° / 50° | 40° / 60° | 40° / 60° |
| Legs | 420-620 | 400-620 | 420-620 | 360-520 |
| Course features | - | - | Switchback's prism walls along legs | Switchback's danger prisms beside the line |

Measured over 400 seeds per level: the walk never fails. Hairpin share is 0 / 50 / 32 / 50%, and the shortest leg
is 420 / 400 / 420 / 360 u.

## What is reused, and what is new

- **Reused whole:** `SwitchbackController` (`ScarabRallyController` subclasses it and overrides only the name and
  the course source), `SwitchbackCourse.Generate`, `SwitchbackFeatures` (I3/I4), the Regatta AI steering
  (`AICommitFromApproachPoint`), `RaceGateTurnMonitor`, `GateRaceScoringRuleSO`, `RaceGateObjectiveProvider` and the
  race-beat toasts.
- **New:**
  - `ScarabRallyCourse`: the Scarab's constants and the ladder.
  - `ScarabRallyCourseSource`: the walk, with Switchback's back-off.
  - Their tests, the scene (a clone of `MinigameSwitchback`), the card, the rule, the preview and the toasts.
  - All the assets come from `Tools/Build/author_scarab_rally_assets.py` (on `hull_time_race.py`).

## AI

- The Scarab's AI holds full throttle (`ScarabVesselTransformer`: under autopilot the trigger reads as held) and
  can snap-dash. It does not drift or juke, so it races the line at 216 u/s on a ~102 u circle.
- Its approach uses Switchback's numbers (commit 260, lead 300, through 220), all outside twice that circle.
- Nobody has raced it yet. The learning sweep picks up recorded races automatically (`AUTO_LEARNING.md`).

## Known limitations

- Not run in the editor. The scene is a text clone of Switchback's. It needs one open-and-save in Unity (the
  Editor then owns its fileIDs), and then `author_scarab_rally_assets.py --check` adopts it.
- Card art is the placeholder until `/cardart` renders the mode. Its renderer needs a `ScarabRally` recipe; the
  Switchback branch is the one to copy.
- The juke is not measured against an actual ring entry. The 40 u figure is `jukeSpeed x jukeDurationSeconds`.
