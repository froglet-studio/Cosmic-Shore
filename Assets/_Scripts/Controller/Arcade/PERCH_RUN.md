# Perch Run - the Goby's Time race

> **Status.** Authored headless on 2026-11-07 (`claude/goby-perch-run`), for Garrett's ask that every vessel get a Time
> game that suits it. The course, the AI's dart plan and their tests were compiled and RUN outside the editor (400 seeds
> per intensity, `Tools/Build/perch_run_harness/run.sh`), and the mode compiles against Unity's reference assemblies.
> **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.PerchRun = 81` is the **Goby's Time card**.

## 1. Why this race, for this hull

The Goby (`Crew/GobyPistol`, `.claude/skills/duo-vessel`) is a crew vessel. The Goby half cruises on the two-thumb
flight and **darts**:

- **Hold** the dart button to charge a length: 140 u untouched, 600 u at a full charge after 0.8 s (`dartMinRange`,
  `dartRange`, `dartChargeSeconds`).
- **Let go** and it flies there in **one second** (`dartSeconds`), whatever the length. Then a **1.2 s** reload
  (`dartCooldown`).
- It **stops on the first block on its line** (`FirstOnLine`, padded by `hullRadius` 3 u). With the Time upgrade
  (Time 5) it phases through.
- A dart that ends 900 u or more from the last perch, with fewer than three islands queued, is a **perch**: the Goby is
  pinned for 1.5 s while the Pistol's island is laid.

A dart costs the same second whatever its length. So the clock of a Goby race is **how many darts it takes**, and each
dart has exactly two numbers a person chooses: its **length** (how long they hold) and its **line** (where the nose
points). Perch Run is built to make those two numbers the decisions:

- a **hairpin** punishes a dart that is too long: overshoot the mouth and the next ring is out of one dart's reach;
- a **coral head** on a leg punishes a dart on the lazy line: aim at the ring's centre and the head stops you short;
  thread the **rim** instead;
- a **double** rewards the line that sees two rings at once: one dart, two rings.

The vessel kit is untouched (the LOCKED mode rule). The mode's levers are the rings, the coral and the AI's coach.

## 2. The ladder (`PerchRunCourse.ForIntensity`, held by `PerchRunCourseTests`)

16 rings, flown once, every leg one dart long (the longest leg plus the overshoot fits the 600 u dart).

| | I1: darts | I2: hairpins | I3: coral | I4: reef |
|---|---|---|---|---|
| mouth radius | 72 | 66 | 60 | 54 |
| plain legs | 300-500 u, bends 15-40° | 300-520, 15-40° | 320-540, 20-45° | 340-560, 20-50° |
| roles repeat every | - | 2 rings | 4 rings | 5 rings |
| hairpins | none | every other ring, 105-140° | every fourth, 105-140° | every fifth, 110-150° |
| coral blockers | none | none | every fourth leg | every fifth leg |
| doubles | none | none | none | every fifth ring |
| coral head | - | - | 30 u cube | 26 u cube |

- **Hairpin.** A short leg in (300-340 u), a turn back of 105-150°, and a long leg out (510-540 u). A full charge
  through the hairpin's ring overshoots it by 260 u or more, and from there the next ring is more than a dart away: two
  darts. Charged to just past the mouth, the dart out reaches it: one. The test holds both halves on every hairpin of
  1,200 courses (`Every_hairpin_punishes_a_full_charge`).
- **Blocker.** A coral head 62% of the way along the leg, a face square to it. A dart from the ring before at the
  ring's centre stops on it, short of the ring; a rim shot (a line inside the mouth that clears the head) threads the
  ring untouched (`Every_blocker_stops_the_centre_line_and_leaves_a_rim_shot`).
- **Double.** A short leg in (240-300 u) and the second ring straight on down the line from the ring before (200-240 u
  on). One dart from the ring before threads both (`Every_double_is_one_dart`).
- Every leg clears every other ring and every coral head but its own, every ring sits in the race cell's shell outside
  the nucleus, and the course is the same for the same seed on every peer.

Why I3 has fewer hairpins than I2: I2 is the length lesson, so it is half hairpins (a zigzag). I3 is the line lesson,
and a course that asks both at every ring is noise; I3 and I4 mix all of them at a rhythm a person can read.

### What it measures (offline, 400 seeds, `run.sh --sweep`)

Three pilots on the seat AI's numbers (turn 90°/s, cruise 100 u/s, the shipped dart):

| | cruiser (never darts) | full-charger (every dart at the centre, full charge) | reader (the AI's `PlanDart`) | the reader saves |
|---|---|---|---|---|
| I1 | 67.7 s | 38.8 s | 36.0 s, 16 darts | 7% |
| I2 | 84.0 s | 48.2 s | 37.8 s, 16 darts | **22%** |
| I3 | 78.2 s | 49.1 s | 36.8 s, 16 darts | **25%** |
| I4 | 68.2 s | 48.5 s | 30.5 s, 13 darts | **37%** |

The lazy pilot gets slower as the levels go up and the reading pilot does not, so each level is worth learning: the
gap between "holds the button" and "reads the dart" grows from 7% to 37%. A cruiser is never competitive: the dart is
the race.

**What was tried and dropped.** The first cut put a coral **backstop** just past each hairpin's ring, so a full dart
braked in the mouth. Measured, it helped a careless pilot as much as a careful one (the full-charger gained 6.5 s at
I2), so the reader saved only 5-9% at I2. It was removed: the hairpin's lesson is the length, and the vessel already
shows the length (§4).

## 3. The coral

`PerchRunController.OnCourseRaised` lays one **super-shielded** SpawnablePrism per blocker (Domains.Blue, owner
"PerchRun") at `Coral.Rotation`, scaled to the cube side, on every peer, from the broadcast rings
(`PerchRunCourse.CoralFor`): nothing new is replicated. The Goby's dart stops on any prism not its own
(`Perchable`), so a coral head is just a block; a dart that stops on one is a dash or a perch like any other.

## 4. The vessel changes (GobyPistolDuo; the Goby is Garrett's)

Two, both kept to what a person already has:

1. **The reach ring.** While the person charges a dart, a small lit ring stands where the dart will stop: at the
   charged length along the nose, or on the first block on the line (none with Time 5, which phases through)
   (`TryDartReach`, drawn in `Draw3D` with `_reachMat`). The charge gauge says how much; the reach ring says where. The
   length of a dart is the Goby's whole skill in every mode, and without the ring a person judges 140-600 u of open
   water by eye.
2. **A race coach for the AI** (`IGobyRaceCoach`, `GobyPistolDuo.RaceCoach`). With a coach set, the seat's Goby AI
   races on the coach's darts instead of laying islands for the Pistol, through the same calls: it turns the hull at
   `aiTurnRate`, cruises at `aiCruiseSpeed`, and fires `FireDart` (the call the button makes) at the coach's length once
   the nose is within 0.5° of the coach's aim. Without a coach the shipped AI is unchanged. A person's Goby ignores it.

## 5. AI

`PerchRunController` is every AI Goby's coach (set as each racer is armed, server). `TryRaceAim` returns
`PerchRunCourse.PlanDart` from the hull at its next ring:

1. **A double first**: if a line inside both mouths reaches the ring after next within one dart and clears the coral,
   that line, charged to 40 u (`Overshoot`) past the second ring.
2. Else **this ring**: its centre if that line is clear, else the nearest clear rim point (24 directions x 6 radii
   inside the mouth, `TryRimShot`), charged to 40 u past the ring, clamped to 140-600.
3. Length 0 only when no line clears: it cruises (never seen in the 6,400 test courses).

The AI never fires a dart that stops on coral (`The_AI_dart_threads_every_ring`, all 400 seeds x 4 levels x 16 rings).
Not verified in the editor: that the turn settles within 0.5° at low frame rates, and how the Pistol's island laying
looks when the Goby perches mid-race.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (rings) | FrogletTools > Game Modes > End Game Conditions, "Perch Run - Gate Target" (`perchRunGateTarget`) | **16** |
| the ladder | `PerchRunCourse.ForIntensity` | §2 |
| Goby numbers the cut depends on | `PerchRunCourse` constants, read back off `GobyPistolConfig.asset` by `The_course_is_cut_for_the_shipped_goby` | 600 / 140 / 0.8 / 1 / 1.2 / 3 |
| detection clamp | scene > `maxPlausibleSpeed` | **1400** (a 600 u/s dart, twice, with room) |
| comeback rate | `ArcadeGamePerchRun.asset` | **0.25** (Time below 5 does nothing to the dart; it shortens the Pistol's jump) |

## 7. Assets

All authored by `Tools/Build/author_perch_run_assets.py` (on `hull_time_race.py`, Thresher Time's scene as the donor):
the card, the scoring rule (`GateRaceScoringRuleSO`), the scene, the preview, the toasts, the registrations. `--check`
is the drift gate. Card art: `render_card_backgrounds.py PerchRun` (an I4 course with its coral).

## 8. In-editor verification (owed)

1. Open the scene once and save it (it is a text clone); then `author_perch_run_assets.py --check`.
2. I1, one Goby: hold the dart; the reach ring slides out along the nose as it charges; let go and the Goby stops on
   it. Darting through a ring counts it.
3. I2: full-charge through a hairpin and the next ring is out of reach; charge to just past the mouth and one dart
   turns it.
4. I3: the coral heads stand on their legs; a dart at the centre stops on the head (the reach ring sits on the head
   while charging); a rim shot threads.
5. I4: a double, darted from the ring before on the right line, counts both rings.
6. Two AI Gobys: they dart ring to ring and finish; note any ring where one turns in place or cruises.
7. MPPM two clients: both see the same rings and coral.

## 9. Known limitations

- **Thinking time is not modelled.** The clock above is the vessel's only; a person's ~2.3 s a dart is closer to 3-4 s
  while learning.
- **The Pistol rides along.** A person flying the Pistol in this race has nothing to race with; the race is the Goby's.
  The seat swap is the shipped kit, so it is left as it is.
- **Time 5 phases through coral.** A trailer would need 20 rings of deficit at the 0.25 rate to reach it, so in a
  16-ring race it never does.
