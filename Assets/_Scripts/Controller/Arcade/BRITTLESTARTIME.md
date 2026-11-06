# Brittle Star Time - Technical Documentation

> **Status.** Authored headless on 2026-10-27. The course and its tests were compiled and RUN outside the editor
> (400 seeds per intensity). **Not run in the Unity editor; nobody has flown it.**

## Overview

`GameModes.BrittleStarTime = 74` is the **Brittle Star's Time card**: a lapped race of eight rings, flown for two laps,
first domain home wins on finish time. Its four intensities are **four different movement questions** about Speed Mode
and the turn (Shombith, 2026-11-06: "I1 Gearbox, I2 Feather slalom, I3 Coast-in hairpins, I4 Corkscrew"). Nothing on
any rung asks for the guns, the cords, the ring or Creation: Time and movement only.

> **Status.** Re-cut headless on 2026-11-06 (`claude/brittle-stars-l5-updates-41j91p`). The course and its tests were
> compiled and RUN outside the editor (`Tools/Build/brittle_star_time_course_harness/run.sh`, 400 seeds per rung, four
> negative controls). **Not run in the Unity editor; nobody has flown it.**

## The hull's movement, in numbers (`BrittleStarTimeCourse`; the tests read each back off the shipped assets)

| | speed | turn circle |
|---|---|---|
| Cruise | 45 u/s | ~31 u |
| Speed Mode (A), x6 | 270 u/s | ~145 u |
| Speed Mode at Time 5, x7 | 315 u/s | ~162 u |
| ... rolling and pulling (Speed Roll, the Time 5 upgrade) | 315 u/s | ~90 u |

Turn rate is `0.1 x speed + 80` deg/s; Speed Roll's pull adds 90 deg/s. Speed eases toward its target at 1.5 a second
(`VesselTransformer.LERP_AMOUNT`), so letting go of Speed Mode is a coast-down, not a stop: there is no brake.

## The ladder (`BrittleStarTimeCourse.BuildLap`; `BrittleStarTimeCourseTests`)

| | I1 Gearbox | I2 Feather slalom | I3 Coast-in hairpins | I4 Corkscrew |
|---|---|---|---|---|
| **The question** | Hold Speed Mode through this corner, or shift down to Cruise? | Neither mode flies the weave: tap A in and out to ride a speed between | Let go of Speed Mode early enough to coast into the hairpin, then light it again | Roll to hold Speed Mode through a coil it cannot turn flat |
| **The course** | Headlong's solver, a median of 2 corners a lap tighter than Speed Mode's circle | 8 rings alternately 90 u outside and inside a 300 u loop: four weaves a lap | A paperclip: 600 u straights 90 u apart, a ring at each hairpin's apex facing across | 6 rings a quarter turn apart on a 100 u coil, then a 200 u-radius return flown flat out |
| **Measured** | floor 65 u | bend 79 u on the half-mouth line, held at ~129 u/s; even brushing the rims 133 u (< Speed's 145) | widest line 89 u, held at 147 u/s: let go ~106 u before the turn | bend 115-133 u on every line: tighter than Speed at Time 5 (162), wider than rolling (90) |
| **Mouth** | 80 | 60 | 44 | 42 |
| **Start** | rest | rest | rest | **Time 5** (the card's `StartingElements` row, intensity 4): Speed Roll is live and Speed Mode runs x7 |

Every rung lays gate 0 on the spawn pole and lies in the plane square to the pole, 700 u from the cell's centre, turned
by the seed (and mirrored by it at I3 and I4). A rung whose shape cannot be laid in the shell, or a race target that is
not two laps of eight, flies the Gearbox instead, so the race always has a course.

**Why the corkscrew is a section and not a lap.** Eight rings round a whole-lap coil sample it too coarsely: five turns
read as a lazier three-turn spiral the other way, which a pilot could fly instead. The coil's rings sit a quarter turn
apart (`CoilStepDegrees`, held by a test and a negative control).

**How the rings are judged.** A ring counts any pass through its plane inside its mouth, in either direction
(`RaceGateRing.PlaneCrossing`). So every rung is measured on the lines a pilot can actually fly: through the centres,
through the half-mouth line, and brushing every rim.

## Reused and new

As for Serpent Time: Headlong's solver (I1), the shared gate race (`GateRaceController`), the card, rule, toasts,
preview and scene from `Tools/Build/author_brittle_star_time_assets.py` (on `hull_time_race.author`, which gained an
optional `card_extra` for the StartingElements table). The card art is rendered from the real course
(`render_card_backgrounds.py BrittleStarTime`, intensity 2). The AI flies Speed Mode with the hull's shipped
`BrittleStarSpeedModeAIPolicySO`.

## Known limitations

- **Not run in the editor.** The scene needs one open-and-save.
- **The AI cannot roll.** The Brittle Star's autopilot never reads the right stick, so on I4 it cannot use Speed Roll:
  it drops to Cruise through the coil and loses time. Teaching it is a change to the vessel's AI (Garrett's kit, trained
  through Yash's pipeline and Froglet Compute), not to this mode.
- **The AI's approach is sized for I1.** Its approach numbers (commit 320, lead 360, through 250) suit Speed Mode's
  circle on long legs; the slalom's rings are ~240 u apart and the coil's ~170 u. Its Time genome is to be trained on all
  four rungs through Compute (Plan step: time-arcade), never by hand.
- **Variety per seed is orientation only** on I2-I4 (a turn about the pole, a mirror): the shapes' numbers are the
  design, so they do not jitter.
- The Brittle Star's prefab has been stable for months. The two old branches that touched it
  (`align-brittlestar-falcon`, `falcon-brittlestar-reintro`) are stale.

## Superseded plan: four intensities, four races (proposed 2026-11-02, never built)

> **Superseded 2026-11-06** by the movement ladder above: Shombith asked for Time and movement only, so the rungs no
> longer put the guns, the cords, the ring or Creation at the front. Kept as the record of what was considered.

Asked for (2026-11-02): "level it up by a lot, all 4 intensities should be different experiences". Today every rung is
the same race with tighter corners and smaller mouths, and it asks only one question: Speed Mode or Cruise. The
Brittle Star has four modes plus the ring ("an all-in-one tutorial on the more advanced systems of the game"), so each
rung puts one of them at the front and stands in its own place. The vessel's original design already ladders its
*Demolition Derby* this way (I1 obstacles, I2 walls, I3 clusters past a crystal for a ring, I4 super-shielded walls,
`Docs/Studios/BRITTLE_STAR.md` §2); this plan carries that ladder into the Time race. Serpentine (`SERPENTTIME.md`) is
the precedent for every mechanism here.

**Rules it keeps.** The vessel kit is never changed (LOCKED, `/arcadegame` §6): no vessel file, prefab, action SO or
config is edited. The mode's levers are the track, what is laid on it, the place (cell and fauna), crystal placement and
its AI's choices. Everything laid is neutral (`Domains.Blue`), laid on every peer from the course seed (Serpentine's
pattern, so copies agree), and grows back on a timer once no hull is near, so lap 2 asks again.

### Facts of the shipped kit the ladder is built on

- **Speed Mode (A)** is ×6 cruise (270 u/s) on a ~145 u circle. **Space, Charge and Creation fly at cruise (45 u/s)**,
  Creation at half of it: every mode other than Speed costs pace.
- **RT in Speed Mode fires one turret** (90 rounds/s, never overheats alone); **Space Mode (X)** fires all five, meeting
  120 u ahead, 387 u range, overheating in ~6 s, and an overheated Space Mode trail lays **danger prisms** behind you.
- **A round stops at its first prism**: guns punch holes in a flat wall but are eaten by a deep clump.
- **Charge Mode's cords (Y)** slice everything in their sweep, out to 120 u (whip) / 80 u (saber), and cool the guns.
- **The ring (LT) blows on touching mass in a disk in its own plane, as wide as the nucleus (~780 u) and only 1.8-7 u
  thick.** Thrown at a flat wall across the path, it lies in the wall's plane and wipes the whole wall; thrown at a 3D
  clump it cuts one slice. It needs a spare ring, and **only crystals give spares**.
- **Ramming a prism costs speed** (the hull's container carries the prism slow and the boost reset); a **danger prism
  drains petals**, and Time petals are Speed Mode's ×6 → ×8.
- **Super-shielded mass refuses guns, cords and the ring.**
- **The autopilot already reacts to neutral mass**: it counts any prism not of its domain on a thin line ahead (8 u
  radius for the guns, 12 u for the cords), takes Space for a far wall, Charge for a near one, and throws a spare into a
  cluster of 18+. So the laid mass is something the shipped AI already plays.

### The ladder

| | I1 Open Water | I2 Breakers | I3 The Reef | I4 The Fortress |
|---|---|---|---|---|
| **The question** | Hold Speed through this corner or drop to Cruise? | A wall across the leg: shoot it, wipe it, or swing round it? | A thicket on the line: cut through it or fly round it? | A wall you cannot break: thread its hole, and plug it behind you? |
| **Mode at the front** | Speed (A) | Space (X), the ring (LT) | Charge (Y) | Creation (B), and all of them |
| **What is laid** | Sparse single drifting blocks on the straights: the lone turret's practice in Speed Mode | 1-2 flat plain walls a leg, 400+ u before the next gate; some leave a side gap a wide line can take | 3D thickets round half the gates, off the line where cutting through saves the corner | Super-shielded baffles with 2-3 holes (hole radius >= 14 u, so the AI's 8-12 u scan line through a hole sees nothing and it does not shoot the baffle); some holes plugged with plain mass; danger rims |
| **Crystals on the line** (`CrystalManager.SetCourseKeys`, Switchback's) | none | one before each walled leg | one a lap, before the biggest reef | two a lap |
| **Corners tighter than Speed's circle** | 0 | 1 | 2 | 3 (today's ladder, kept) |
| **Mouth radius** | 90 | 68 | 54 | 42 (today's) |
| **The place** (cell config + fauna, Serpentine's generator pattern) | Skim Race's barren shallows, a few tadpoles | debris belts, grazers | the reef: brittle star fauna that graze the thickets, opening holes over the race | the deep: sharks and piranha |

**What each rung teaches, in play.** I1: A on the straights, the single turret clears a block without leaving Speed.
I2: dropping out of Speed to fire five guns costs pace (cruise is a sixth of Speed); the one turret in Speed Mode is too
thin to open a wall in time; a crystal's ring wipes the whole wall in one throw, and anything else in that plane, for
everyone behind you too; five guns held too long overheat and leave danger prisms for the pilot on your tail. I3: guns
are eaten by depth, the ring only slices a layer, so the cords (at cruise) are the answer, and they also cool guns run
hot on the way in. I4: precision at speed (a ~145 u Speed circle against small holes means Cruise for the line-up),
and the dirty play: dropping into Creation in a hole lays five trails across it for whoever is behind, who must cut it.

### Phases

1. **The obstacle layer** (`BrittleStarTimeObstacles`, pure: course + seed + intensity in, `PrismLay` lists out, no Unity
   object), tested like `SerpentineGate`: walls span the leg, gaps and holes stay clear of a hull, every corner is still
   flyable at cruise, nothing sits inside a mouth, two gates' structures never meet, and the prism budget holds. The
   controller lays it on every peer from the course (`OnCourseRaised`) and regrows it on a budgeted timer.
2. **Crystals on the line** per the table, through `CrystalManager.SetCourseKeys` (server; clients follow).
3. **Four places**: four cell configs (IntensityWise) and their spawn profiles, copied from existing configs with only
   their numbers changed, in `author_brittle_star_time_assets.py` (Serpentine's `OWN_CELLS`). Card description, preview
   objective and one start hint per rung as authored text (the start hint is a toast; no literal strings).
4. **The AI, mode side only**: aim through a baffle's hole (`TryOverrideAim`), and at I4 press Creation in a hole when a
   rival is close behind (the replicated press, as Serpentine's AI presses the Serpent's). Everything else is the hull's
   own autopilot. Then the Brittle Star's Time genome is trained on all four rungs at once through Froglet Compute
   (`/compute-job`, **Plan step: time-arcade**), never by hand.
5. **Gates and paper trail**: the course tests extended per rung, the generator's `--check`, the reference compile, the
   verification checklist entry, this doc.

**Performance budget** (Serpentine's lesson, "the performance is very very bad"): at most ~2,500 laid prisms a course,
no per-frame sweeps (regrow on a timer, a few dozen a frame), markers on the lay and the regrow. Four AI Brittle Stars
bursting Space Mode at walls keep ~2,000 rounds alive each (`R_VesselActions/BRITTLE_STAR.md` F9): profile I2 first.

### Open decisions (the asker's)

1. The four places and their names as above?
2. I1 with no crystals (pure Speed), or one a lap so the ring stock is learned early?
3. Two laps on every rung, or fewer laps where a lap is long and dense (I3, I4)?
4. May the I4 AI play dirty (drop Creation in a hole ahead of a rival)?

Owners: the Brittle Star's kit is Garrett's (he played and tuned it; this plan edits none of it). The AI training goes
through `Ys-bleeding-edge` and Compute (Yash).
