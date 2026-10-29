# Lantern Run - Technical Documentation

> **Status.** Authored headless on 2026-10-29 (`cece/confident-goodall-6cpxan`). The course, the lanterns and their
> tests were compiled and RUN outside the editor (400 seeds per intensity, against the card-art harness's Unity shim),
> and the C# compiles against real Unity references. **Nothing here has been run in the Unity editor, and nobody has
> flown it.**

## Overview

`GameModes.LanternRun = 75` is the **Angler's Time card**. Garrett, 2026-10-29: "angler seems like a fun one. lets make
a great angler time game." It is a two-lap circuit of eight rings on Headlong's shared solver, and the first DOMAIN
whose lead runner threads the last ring wins (golf: finish time).

## The loop: you travel by your school

The Angler cruises at 40 u/s on a flat 48°/s turn, the slowest hull in the fleet. Its speed is its tamed school, used
through the four controls it already has (`R_VesselActions/ANGLER.md`):

| Verb | Control | In this race |
|---|---|---|
| **Paint** (Space) | hold RT | The group whose turn it is balls up at your nose, and the 3° brush lights whatever lies along it. Aim it through the next ring at the **lantern** |
| **Throw** | let go of RT | That group flies at the lantern at 150 u/s, homing (x2 with Time). With nothing painted it flies along the nose, decaying: about 316 u |
| **Dash** (Time) | B | The hull covers 75% of the way to a group in 0.35 s. Dash after the thrown group, again and again, and you move at the school's pace (about 4x your cruise). Lined up right, the dash line runs **through the ring's mouth** |
| **Lure** (Mass) | A | Tame wild herbivores into the school. Omni crystals in the cell add a creature each |
| **Lock-on** (Charge) | LT | Spend a creature to hunt a rival: each bite takes petals |

The two groups take turns (ANGLER.md §1m): paint, throw, dash; then paint the next lantern, throw the other group,
dash. Nothing is ever recalled.

**A lantern** is a small ball of 18 neutral (Blue) prisms hung 140 u past a ring, on the line you arrive along. Any
Angler can paint it and any creature can eat it. Rivals' trails are enemy prisms too, so they paint as well: a trailing
pilot can throw at the leader's trail.

## The ladder (`LanternRunCourse`, asserted by `LanternRunCourseTests`)

| | I1: learn the leapfrog | I2: fly the line | I3: plain throws | I4: gauntlet |
|---|---|---|---|---|
| Lanterns | every ring, on the arrival line | every ring, 1.4 mouths to the side | every 2nd ring, on the line | every 3rd ring, to the side |
| Mouth radius | 72 | 58 | 46 | 36 |
| What it asks | aim through the ring, throw, dash | the straight line paints nothing: fly the line through the mouth to the lantern, then throw | between lanterns, plain throws (316 u) or a rival's trail | all of it, with the tightest mouths |

Every corner floor (110-180 u) is far above the 48 u cruise circle: the Angler's corners cost nothing, because its
speed is the throw and not the hull.

## What is new beyond the mode

- **The throw is fixed** (ANGLER.md follow-up 11). `LightFauna` and `Boid` used to clamp a leashed creature's
  velocity back to its wild top speed on every behaviour tick, so a 150 u/s throw collapsed to ~30 u/s. Now
  `Fauna.LeashOwnsSpeed` leaves the motion to the leash's per-frame steer whenever the leash asks for more than the
  creature's own top speed: a throw, a hunt (towed x1.6) and the lure's orbit. Orders at or under the creature's top
  speed (follow, gather, eat) keep the flock's steering exactly as before. The Angler is the only leash holder (`IFaunaLeash`); the Jacks
  herd through `IFaunaHerd` and are untouched.
- **An Angler AI** (ANGLER.md follow-up 2): `AnglerLeapfrogAIPolicySO` on the hull (`AnglerAIBoostPolicy.asset`,
  authored by `author_angler_assets.py`). Input-only:
  - lined up within 4° of the next ring, it holds RT for 0.45 s and lets go;
  - it presses Dash whenever the next dash (`AnglerCommandExecutor.TryGetDashPoint`, a read) carries it 40 u or more,
    within 50° of the objective.
  - It does not lure, erase or lock on.
  - It replaces the Stoat's skim-ring policy, inherited with the hull clone, which bound nothing on an Angler.

## Reused

`GateRaceController`, `HeadlongCircuit`, `RaceGateTurnMonitor`, `GateRaceScoringRuleSO`, `RaceGateObjectiveProvider`,
the race-beat toasts, and the barren race cell. The lanterns use Switchback's course-features shape
(`SpawnableLanternRunLanterns`, laid on every peer from the broadcast course). All the assets come from
`Tools/Build/author_lantern_run_assets.py` (on `hull_time_race.py`).

## Known limitations

- **Not run in the editor.** The scene is a text clone of `MinigameThresherTime`, and needs one open-and-save.
- **A CLIENT pilot cannot dash** (ANGLER.md follow-up 7): creatures are leashed on the host, so a client's own
  executor holds no school. Solo, the host and AI rivals are unaffected. Online races with a remote Angler need the
  school replicated to its pilot first. **This is the biggest gap before Lantern Run works online.**
- **Lanterns are eaten.** A creature eats the prism it reaches. Laid on every peer, a lantern is eaten on the host
  (the fauna's authority), and a client keeps drawing it. With 18 prisms per lantern and half a school per throw,
  four racers can each throw at every lantern, but a long race could empty one.
- **Race time is an estimate:** about 70-80 s at I1 (eight ~600 u legs a lap at the school's 150 u/s, plus a ~0.5 s
  wind-up per throw) and about 100 s at I4. Nobody has raced it.
- The AI never lures, so it races with its keeper plus omni crystals. That is enough to leapfrog, but a human who
  tames a bigger school has more creatures to spend on Charge bites.
