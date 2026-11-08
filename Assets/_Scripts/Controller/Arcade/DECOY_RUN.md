# Decoy Run - the Jacks' Time race

> **Status.** Authored headless on 2026-11-07 (`claude/jacks-decoy-run`), for Garrett's ask that every vessel get a
> Time game that suits it. The course, the AI's cycle plan and their tests were compiled and RUN outside the editor
> (400 seeds per intensity, `Tools/Build/decoy_run_harness/run.sh`), and the mode compiles against Unity's reference
> assemblies. **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.DecoyRun = 80` is the **Jacks' Time card**.

## 1. Why this race, for this hull

The Jacks fly at the Rhino rig's plain **50 u/s** (`R_VesselActions/JACKS.md`). Every useful thing they do happens in
the **herd-and-travel cycle**:

1. **Swap to the jacks** (the seat swap, `PilotSwap.CooldownSeconds` 0.75 s). The hull holds still and the two jacks
   run on its board (the plane through the hull, square to its up) at **208 u/s** (`dogSpeed` 520 x `labScale` 0.4).
2. **Lay a decoy** with a trigger: it lands where that side's jack stands. One decoy; Time 5 gives each trigger its own.
3. **Take off** (swap back): the hull **jumps** to halfway between the jacks, facing their average heading. The jump is
   `VesselTransformer.SetPose`, a **teleport**, and a gate race never credits a teleport (`GateRaceController`'s
   `TeleportCount` check, written for the Butterfly's Fold). So **the take-off threads nothing: it only puts the hull on
   a line.**
4. **Fly to the decoy** with a trigger: a real flight at up to **900 u/s** (any distance in 0.35-1.2 s), with a 0.6 s
   guard before the next. That flight threads what it passes.

So a Jacks race is not about flying. It is about **two thumbs on a board**: one jack lays the decoy past the ring, and
both jacks end where their midpoint sits behind the ring on its line, so the take-off lines the hull up and the
travel shoots it through. Every ring costs one herd (the jacks' run, the swap cooldown) and one travel. Decoy Run is
built from exactly that:

- rings come in **groups one cycle can take**, so a pilot who reads the course runs fewer cycles;
- the board is a **plane**, so from I3 each group lies on its own **flat**, and the hull has to roll onto it first;
- the **chalk** shows each flat: two dotted rails beside every leg, on that leg's flat.

The vessel is untouched (the LOCKED mode rule): the run, the decoys, the take-off, the travel and the swap are the
shipped kit. The mode's levers are the rings, the chalk, the card's StartingElements and the AI's coach.

## 2. The ladder (`DecoyRunCourse.ForIntensity`, held by `DecoyRunCourseTests`)

| | I1: doors | I2: skewers | I3: tilt | I4: knots |
|---|---|---|---|---|
| New thing | herd, decoy, take off, travel | two rings on one line: one travel takes both | each skewer on its own flat | Time 5 (the second decoy) and a hairpin |
| A group | 1 ring | 2 (a skewer) | 2 (a skewer) | 3 (a skewer, then a hairpin) |
| Ring radius | 72 | 62 | 56 | 48 |
| Skewer (leg, bend) | - | 300-440 u, 0-4° | 300-420 u, 0-4° | 280-400 u, 0-4° |
| Hairpin (leg, bend) | - | - | - | 260-380 u, 125-155° |
| Turn into a group (leg, bend) | 380-560 u, 30-75° | 360-540 u, 50-100° | 340-520 u, 50-100° | 320-480 u, 40-90° |
| Flats | one (the start flat) | one | rolled 25-45° per group | rolled 30-55° per group |
| Start | Time 0 | Time 0 | Time 0 | **Time 5** (card) |

Twelve rings at every level (the end condition, one count).

**The start flat.** Every pilot spawns abreast, 260 u behind ring 0, flying +X with the cell's up as its up, on a disc
640 u above the cell's centre (clear of the core). So every pilot's board IS flat 0 from the first frame.

**The skewer (I2+).** A group's first two rings sit on one straight line and both face down it (a skewer's first ring
faces the skewer, not the way it was entered). The turn INTO a group is hard (50-100°), so the skill is the line: put
the take-off behind the first ring on the skewer's line and the decoy past the second. One travel then takes both.

**The flats (I3+).** Flat k contains the last ring of flat k-1 and its axis (the HINGE), rolled 25-45° (I3) or
30-55° (I4) about that line, so a hull that leaves the hinge along its line is on both flats at once and only has to
ROLL (one fish up, one down) to lay its board onto the next. A group's rings sit within 45% of their radius of the
board when the board is on their flat, which is the AI's test for "on the board". The flats are a function of the
broadcast rings (`DecoyRunCourse.FlatNormal`), so nothing new is replicated.

**The hairpin (I4).** After the skewer the third ring sits 125-155° back, beside the skewer, so the second travel
doubles back from the first decoy through it. The jack that lays the second decoy therefore has a short run, which is
what makes a three-ring cycle worth it.

**The comeback.** Time only matters here at 5 (the second decoy), so the rate is a level a ring (`1.0`): five rings
behind hands a trailer the second decoy, and with it one more ring a cycle.

### What it measures (offline, 400 seeds, `run.sh --sweep`)

Race clock of a pilot who acts the moment it can (thinking time NOT counted). A flat change costs the roll (90°/s,
the rig's roll rate) plus a second for the board to settle, less the swap's 0.75 s it overlaps.

| | flyer (no herding) | one ring a cycle | the AI's plan (a group a cycle) | saved | cycles | longest run |
|---|---|---|---|---|---|---|
| I1 | 106 s | 35.9 s | 35.9 s | 0% | 12 | 2.7 s |
| I2 | 93 s | 32.5 s | 28.0 s | 14% | 6 | 4.4 s |
| I3 | 91 s | 35.0 s | 30.5 s | 13% | 6 | 4.3 s |
| I4 | 81 s | 32.8 s | 26.1 s | 20% | 4 | 4.9 s |

Flying alone is three times slower at every level, so the race is the herd. The groups save **only the cycle
overhead** (the swap cooldowns, the travel guard), because the jacks' running distance is the course's length either
way; for a person, each cycle also costs the time to read and place the thumbs, so the saving for a person is larger
than the AI's. The first decision a level teaches (line two rings up, roll first, use the hairpin) is worth 13-20% on
the clock.

## 3. The chalk

Two dotted rails of super-shielded environment prisms (5 u) beside the start line and every leg, `ring radius + 30 u`
either side, on that leg's flat (a leg into a new flat lies on the new one). A dot is dropped when it comes within a
ring's radius of any leg or of any ring, so no dot sits where a hull flies (`The_chalk_keeps_off_every_mouth_and_every_leg`).
Super-shielded mass is never dragged by the jacks' net (`JacksNet.DragPrisms`), so herding cannot pull the chalk away.
Every peer lays the same chalk from the broadcast rings.

## 4. The vessel changes (JacksExecutor; the Jacks are Garrett's)

Two, both the vessel's and both kept to what a person already has:

1. **The take-off mark.** The herding view draws a small white ring at the jacks' midpoint, where the hull will be when
   they take off (`takeOffColor`). The take-off is a jump, so its landing point is an instrument: without it a person
   has to guess the midpoint of two moving fish, which is the whole skill of this race and of any Jacks travel.
2. **A race coach for the AI** (`IJacksRaceCoach`, `JacksRacePlan`, `JacksExecutor.RaceCoach`). With a coach set, an AI
   Jacks herds and travels on the coach's cycles instead of its herding rhythm (`aiHerdSeconds` / `aiFlightSeconds`),
   every step through the calls a person's controls make: the jacks to points on the board, a trigger to lay a decoy,
   the seat swap under its cooldown, a trigger to fly to a decoy. Without a coach the shipped AI is unchanged. A
   person's hull ignores it.

## 5. AI

`DecoyRunController` is every AI Jacks' coach (set as each racer is armed, server):

- **Flying**, it steers at the next ring (`TryOverrideAim`) and rolls its board onto the next ring's flat
  (`OverrideBellyTarget`: belly square to the flat, whichever face it already shows).
- **The cycle** (`TryNextCycle`): the rings one cycle is cut to take (`RingsACycleTakes`: a door, a skewer, a ring more
  with the second decoy) that lie on its board. It waits, flying and rolling, up to `aiGroupWaitSeconds` (2.5 s) for
  all of them, then herds what does. `DecoyRunCourse.PlanCycle` places the take-off `JumpLead` (70 u) behind the first
  ring on its line, decoy 0 `Overshoot` (45 u) past the last ring of the first travel, decoy 1 past the hairpin ring,
  and the jacks' ends so the longer run is as short as an 11 x 11 sample of the board finds.
- **Mid-cycle** it holds its heading (the hull is held, or travelling), so the steering never swings the board off the
  flat.

Not verified in the editor: that a jack reaches its waypoint within 0.5 u (the executor's arrival test) on every frame
rate, and that the belly roll settles the board within the 2.5 s wait on a 55° flat.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (rings) | FrogletTools > Game Modes > End Game Conditions, "Decoy Run - Gate Target" (`decoyRunGateTarget`) | **12** |
| the ladder | `DecoyRunCourse.ForIntensity` | §2 |
| Jacks numbers the cut depends on | `DecoyRunCourse` constants, read back off `JacksConfig.asset` by `The_course_is_cut_for_the_shipped_jacks` | 208 / 900 / 0.35-1.2 / 0.6 |
| I4's Time 5 | `ArcadeGameDecoyRun.asset` StartingElements | Jacks, intensity 4, Time 0.5 |
| AI group wait | scene > `DecoyRunController.aiGroupWaitSeconds` | 2.5 s |
| detection clamp | scene > `maxPlausibleSpeed` | **2400** (a 900 u/s travel at 60 fps, twice, with room) |
| comeback rate | `ArcadeGameDecoyRun.asset` | **1.0** (five rings behind = Time 5, the second decoy) |

## 7. Assets

All authored by `Tools/Build/author_decoy_run_assets.py` (on `hull_time_race.py`, Thresher Time's scene as the donor):
the card, the scoring rule (`GateRaceScoringRuleSO`, metric 9), the scene, the preview, the toasts, the registrations.
`--check` is the drift gate.

## 8. In-editor verification (owed)

1. Open the scene once and save it (it is a text clone); then `author_decoy_run_assets.py --check`.
2. I1, one Jacks: the rings and the chalk rails lie on one flat; herd past a ring, drop a decoy, centre the jacks behind
   it (the white take-off mark), take off, travel: the ring counts. Taking off THROUGH a ring does not count it.
3. I2: line a skewer up; one travel counts both rings.
4. I3: roll onto the next flat (the chalk shows it); the board lies on it and the skewer is on the board.
5. I4: the hull starts with two decoys; the second travel doubles back through the hairpin and counts it.
6. Two AI Jacks: they herd, take off and travel cycle by cycle and finish. Note any cycle that stalls (a jack short of
   its waypoint) or a board that never settles on a flat.
7. MPPM two clients: both see the same rings and chalk.

## 9. Known limitations

- **Thinking time is not modelled.** The clock above is the vessel's only.
- **The race is long for a person** (an I1 race is ~36 s at machine speed, so ~60-90 s for a person learning it).
- **No offline race model for self-play** beyond the clock in `run.sh --sweep`; the AI learns from recorded races.
