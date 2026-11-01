# SpeedPainter - Technical Documentation

> **Status.** Authored headless on 2026-11-01 (`claude/intelligent-lovelace-ynskrg`). The course, its clusters and
> their tests were compiled and RUN outside the editor (400 seeds per intensity, against the card-art harness's Unity
> shim). **Nothing here has been run in the Unity editor, and nobody has flown it.**

## Overview

`GameModes.SpeedPainter = 78` is the **Angler's Time card**. Shombith, 2026-11-01: *"as per the abilities of the angler
it can paint at two places where there is prism mass clusters and the fauna will go there to eat it and that way angler
can zoom towards where its fauna is and he has two of these as you know so he can in a way paint its path and zoom
towards it. SO a time game would be arena where rings are closer to where mass prism clusters are and we can do a lap
around it."*

It is a two-lap circuit of eight rings on Headlong's shared solver (`HeadlongCircuit`). The first DOMAIN whose lead
runner threads the last ring wins (golf: finish time).

**Built beside Garrett's Lantern Run** (`GameModes.LanternRun = 75`, `cece/confident-goodall-6cpxan`, PR #1644, not
merged), the same idea with one lantern a ring. SpeedPainter takes its own id and its own classes and touches nothing of
Lantern Run's, as Serpent Gulag did beside Serpent Charge (`/arcadegame` §3b). Two pieces of that branch are the
ANGLER's, not the mode's, and SpeedPainter needs them, so they came over **unchanged** (byte-identical hunks, so the two
branches merge cleanly):

- **The throw keeps its speed.** `Fauna.LeashOwnsSpeed` stops `LightFauna` and `Boid` clamping a thrown creature back to
  its wild ~30 u/s (ANGLER.md follow-up 11).
- **The Angler's AI.** `AnglerLeapfrogAIPolicySO` (`AnglerAIBoostPolicy.asset`, wired by `author_angler_assets.py`),
  input-only: lined up within 4 degrees of the next ring it holds RT 0.45 s and lets go, and it presses Dash whenever the
  next dash carries it 40 u or more within 50 degrees of the objective. `AnglerCommandExecutor.TryGetDashPoint` is the read
  it uses.

Whether to keep both Angler Time races, or fold one into the other, is for Garrett and Shombith to decide.

## The loop: paint your path, zoom along it

The Angler cruises at 40 u/s, the slowest hull in the fleet. Its speed is its tamed school, used through the four
controls it already has (`R_VesselActions/ANGLER.md`). **The mode changes nothing about the vessel** (CLAUDE.md, LOCKED):
it lays the clusters, and that is all.

| Verb | Control | In this race |
|---|---|---|
| **Paint** (Space) | hold RT | The group whose turn it is balls up at your nose, and the 3-degree brush lights the cluster it lies along |
| **Throw** | let go of RT | That group flies at the paint at 150 u/s, homing (x2 with Time), and starts eating |
| **Dash** (Time) | B | The hull covers 75% of the way to a group in 0.35 s; the next dash goes to the other group |
| **Lure** (Mass) | A | Tame wild herbivores into the school. Omni crystals in the cell add a creature each |
| **Lock-on** (Charge) | LT | Spend a creature to hunt a rival; each bite takes petals |

**Two clusters a leg, one for each group.** A leg is the run into a ring. Its **near** cluster sits on the ring's own
line (its axis), 300 u before the ring (half the leg on a short one); its **far** cluster sits 150 u past the ring on the
same line. So one leg is:

1. Paint the near cluster, let go: group A is thrown at it.
2. Paint the far cluster through the ring, let go: group B is thrown at it.
3. Dash: to A, on the near cluster.
4. Dash again: to B. This dash runs **through the ring**, and lands past it.

The second dash lands 75% of the way from A to B, which is past the ring whenever `0.25 x (300 + 150) < 150` - the
constants hold it, and `SpeedPainterCourseTests` asserts it on every leg of 400 courses per intensity.

**The clusters sit on the ring's axis, not on the chord from the last ring.** The circuit presents a ring up to 76
degrees off that chord. On the chord, the near-to-far line crossed the ring's plane up to **four mouths** off centre at
I4 (measured over 400 seeds). On the axis, it threads the mouth by construction. So the clusters also draw the line the
ring wants you to fly.

**A cluster is real prism mass.** It is a golden-spiral ball of neutral (Blue) prisms, the pooled environment prism
(`SpawnablePrism`). Any Angler can paint it, and any creature can eat it - wild ones too, which prefer painted prisms
(`PaintedPrey`, ANGLER.md §1c). A rival's trail is enemy mass as well, so it paints.

**The clusters grow back.** Every `RegrowSeconds`, a cluster with an empty slot grows one prism back into it (the
first empty slot, so the ball refills in the same shape on every peer). Every racer, on both laps, has mass to paint.

## The ladder (`SpeedPainterCourse`, asserted by `SpeedPainterCourseTests`)

| | I1 Two strokes | I2 Zigzag | I3 One stroke | I4 Sparse |
|---|---|---|---|---|
| Clusters a leg | near + far | near + far | far only | far; near on every 2nd leg |
| Off the line | on it | 1.2 mouths, near and far on opposite sides | on it | 1.4 mouths, opposite sides |
| Prisms a cluster | 30 | 26 | 22 | 18 |
| Regrows one prism every | 1.0 s | 1.25 s | 1.75 s | 2.5 s |
| Mouth radius | 72 | 60 | 48 | 38 |
| What it asks | paint, throw, paint, throw, dash, dash | aim each stroke: the line through the mouth paints nothing | the other group's throw is a plain sling (~316 u) or a rival's trail | all of it, with the tightest mouths |

Measured over 400 seeds per intensity (the near-to-far dash line, as a share of the mouth radius it misses the centre
by): I1 0, I2 at most 0.40, I4 at most 0.47. The dash lands 37-66 u past the ring. Every corner floor (110-180 u) is far
above the 48 u cruise circle: the Angler's corners cost nothing, because its speed is the throw and not the hull.

**About 0.3% of clusters are lost to the nucleus** (a near cluster whose ring is presented steeply inward: 10 of 6,400 at
I1, 31 of 6,400 at I2, 21 of 4,800 at I4). Such a leg has one cluster.

## What the mode adds, and what it reuses

- **New:** `SpeedPainterCourse` (the cut and the clusters, pure), `SpeedPainterCourseSource`, `SpeedPainterController` (lays
  the clusters on every peer from the broadcast course in `OnCourseRaised`, regrows them in `LateUpdate`, posts the start
  hint), `SpeedPainterCourseTests`; `GameToastSituation.SpeedPainterPaintHint = 158`; the
  `speedPainterGateTarget` end condition (16); the `MiniGameHUD` case; the card's art recipe.
- **Reused:** `GateRaceController`, `HeadlongCircuit`, `RaceGateTurnMonitor`, `GateRaceScoringRuleSO` (metric 9, golf),
  `RaceGateObjectiveProvider`, the race-beat toasts, the barren race cell, and Serpentine's way of laying and regrowing
  mode prisms (`PrismTrailBuilder.LayOne`, the first cohort behind the connecting panel, regrown ones as live ribbons).
- **All the assets come from `Tools/Build/author_speed_painter_assets.py`** (on `hull_time_race.py`): the scene (cloned from
  `MinigameThresherTime`), the card, the rule, the preview, the toasts and the registrations.

## Verification

- `SpeedPainterCourseTests`: 8 of 8 pass outside the editor (UnityShim + a small NUnit stand-in, 400 seeds x 4 intensities).
  A first cut that hung the clusters on the chord from the last ring FAILED the dash-through-the-mouth test (I2 seed 1 missed
  by 6 u), which is why they sit on the ring's axis.
- `author_speed_painter_assets.py --check` passes; changing one field of the card makes it fail and name the file.
- The C# gates pass (switch labels, enum members, console logging, conditional compilation, using directives,
  self-referential locals, game-list scenes, preview spawns, prism bakes).
- **Not run in the Unity editor.** The editor test steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` under SpeedPainter.

## Known limitations

- **Not run in the editor.** The scene is a text clone of `MinigameThresherTime` and needs one open-and-save.
- **A CLIENT pilot cannot dash** (ANGLER.md follow-up 7): creatures are leashed on the host, so a client's own executor
  holds no school. Solo, the host and AI rivals are unaffected. This is Lantern Run's gap too.
- **Clusters are eaten on the host only.** Each peer regrows from what it saw eaten, so a client may draw a prism the host
  has already lost, until it grows back.
- **The AI paints along its nose.** At I1 and I3 (clusters on the line) its throw lights the clusters; at I2 and I4 it
  mostly throws plain slings and dashes after them. It never lures, so it races with its keeper plus omni crystals.
- **Race time is an estimate**: about 70-90 s, eight ~600 u legs a lap at the school's 150 u/s plus the wind-ups. Nobody has
  raced it.
