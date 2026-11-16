# Brittle Star Time - Technical Documentation

> **Status.** Authored headless on 2026-10-27. The course and its tests were compiled and RUN outside the editor
> (400 seeds per intensity). **Not run in the Unity editor; nobody has flown it.**

## 0. The Shift Line (2026-11-16, round 5)

Yash asked for every vessel's Time minigame to be made super fun. This section is the Brittle Star Time pass. It is
upgraded in place, and the Brittle Star kit is untouched: no vessel file, prefab, action SO or config was edited. The
full design record is `Docs/BrittleStarTime/SHIFT_LINE_SPEC.md`: three designs and a three-judge panel.

**Not run in the Unity editor.** Every number below comes from the offline model in
`Tools/Build/brittle_star_time_course_harness`. It flies the shipped course code with the hull's own laws:
- speed eases at 1.5/s after a 0.35 s mode blend;
- turn rate is 0.1 x speed + 80 deg/s;
- Speed Roll adds +90 deg/s at Time 5;
- the AI's nose lags at its follow rate.

Run it with `run.sh --race`. `--check-race` turns the claims below into a gate, and `--self-test` runs that gate with
10 negative controls.

### What the evidence said first

**Recorded play.** Shombith flew two races (2026-11-02), both on the pre-recut course:
- **I1, 175 s.** The first ring took 41 s: he flew 22 u/s in Creation from the spawn pad, 885 u from ring 0, and first
  lit Speed Mode at 114 s.
- **I4, 135 s.** Overshoot loops at 270 u/s cost 10-16 s each.
- The Medium AI finished neither race.

**The AI today, in the model:** 244 / 95 / 124 / 368 s at I1-I4. The hull's Speed law (`BrittleStarAIBoostPolicy`):
- engages only with 1.5 s of runway;
- lets go 0.5 s before every ring, whatever the corner;
- off Speed, the autopilot picks **Creation (x0.5, 22.5 u/s), not Cruise**.

**The question is narrow.** "When do I let go of Speed Mode?" is mostly answered by "when the nose is far off the line",
because a ring counts anywhere in its mouth.
- At I1, I2 and the I4 coil, a person who simply holds Speed beats any per-ring plan.
- Corners tighter than the Speed the hull carries exist only at the I3 hairpin apexes and the I4 return.
- This is Redline's lesson: lift posts were measured and dropped there.

### What was built

**1. Let-go gates** (`BrittleStarTimeLine`; laid by `BrittleStarTimeController.Line`):
- Two columns of three SuperShielded Blue posts, one either side of the line. They are a pair of columns, not a hoop, so
  they never read as a ring.
- Each gate stands where letting go of Speed Mode coasts the hull to the corner's own speed. A corner's speed comes from
  its flyable circle: the inscribed circle of its legs, plus the mouth.
- Gates stand only where a corner needs one: the two I3 apexes and the three rings of the I4 return. None stand on I1,
  I2 or the coil.
- They are laid on every peer from the broadcast course, and regrow one post a tick.
- On a clean shift, the gate lights in the local pilot's colour (`PrismLit`).
- **Measured:** reading them saves 4.4% at I3 and 8.3% at I4, against the best line flown without them. The bar is 3%.

**2. The race coach** (`BrittleStarTimeCoach`; server, `BrittleStarTimeController.Coach`):
- **Speed wish.** Each AI's wish is set through `BrittleStarExecutor.SetAutopilotSpeedWish`, the hull's own autopilot
  control (input only). It is re-asserted every tick.
- **Rule.** Hold Speed while the nose is on the line; let go at the gate; light it again as the nose comes round.
- **One writer.** The hull policy's mode variant `Resources/AIBoostPolicies/BrittleStarAIBoostPolicy_BrittleStarTime`
  sets align 0 deg and runway 999 s, so the coach is the only writer of the wish.
- **Line.** `TryRaceLine` flies the line: down a ring's facing, or at the centre of a ring that faces across its leg.
  The platform's difficulty mistakes and the missed-mouth break-off still apply.
- **Per difficulty and rung:** a nose follow rate (set on every arming, as Redline and Switchback do) and the share of a
  corner's speed it trusts. Both were searched offline (`--tune`).
- **Pace target**, against the model's reading human:

  | Skill | Easy | Medium | Hard | Legendary |
  |---|---|---|---|---|
  | Target pace | 2.0x | 1.5x | 1.25x | 1.05x |

  A person at the controls flies about 1.6x the model (the recorded I1 race at Speed took 5.5 s a ring, against the
  model's 3.4 s).

**3. The heat** (`BrittleStarTimeHeat`; `BrittleStarTimeController.Heat`, local pilot, unscored). A thread is graded by
the speed it carried, not by where it crossed the mouth:
- **Flat out:** a flat-out ring taken through the heart (60% of the mouth) at 80%+ of full Speed.
- **Clean shift:** a let-go ring taken at 80%+ of its corner's speed.
- **What it shows:**
  - a shock ring off the ring;
  - streaks called at 3, 8 and 16;
  - lap splits and a best-lap call;
  - a clean lap runs a chase of flares round the circuit.
- **Toasts 410-419.**

  | Id | When |
  |---|---|
  | 410 | Start hint, I1 and I2 |
  | 411 | A streak is called |
  | 412 | Lap split |
  | 413 | Best lap |
  | 414 | Clean lap |
  | 415 | The first flat-out thread |
  | 416 | The first clean shift |
  | 417 | Start hint, I3: let go at the posts |
  | 418 | Start hint, I4: roll and pull through the coil |
  | 419 | A dark start: no Speed Mode, no ring, 6 s in |

- **Eight FMOD slots**, all shipped EMPTY.

**4. Laps per rung, 2 / 3 / 3 / 4** (`BrittleStarTimeCourse.LapsByIntensity`):
- The I2-I4 shapes are short. At two laps, a reading pilot finished in 25-37 s.
- The authored race (`brittleStarTimeGateTarget`, 16) stays I1's, and the others scale from it, so it is still eight
  rings a lap.

### The numbers (model, 16-24 seeds a rung)

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Laps (rings) | 2 (16) | 3 (24) | 3 (24) | 4 (32) |
| Reading human, model | 49 s | 54 s | 41 s | 48 s |
| ... x1.6, a person (estimate) | ~79 s | ~86 s | ~66 s | ~76 s |
| Let-go gates worth | none stand | none stand | 4.4% | 8.3% |
| AI today | 244 s | 95 s | 124 s | 368 s |
| Coached Easy / Medium / Hard / Legendary | 98 / 74 / 61 / 52 s | 107 / 80 / 67 / 57 s | 82 / 64 / 51 / 42 s | 96 / 74 / 60 / 59 s |

Every coached seat, at every skill, finishes every rung offline. I4 Legendary tops out at about 1.23x: the coil
unrolled, with a nose follow capped at 8/s.

**Measured and cut:**
- **Funnel rails** down each ring's facing. Against a pilot who approaches each ring along its own facing (the ring shows
  it), they saved -2.3 / -4.3 / -0.7% at I1 / I2 / I4.
- **Let-go gates on flat-out rings.** None are laid.
- **A hoop-shaped gate.** The player judge found it read as a ring to thread.

### What only Unity can show (checklist entry, `Docs/UNITY_VERIFICATION_CHECKLIST.md`)

- Not compiled by Unity.
- The posts' look and light.
- That the coach's presses do not chatter.
- The coached AI in a six-seat grid.
- The hints showing.
- The real finish times, which decide the laps.
- That rival autopilots do not shoot the posts. They sit about 55 u off the line, outside the 8-12 u scan, and are
  SuperShielded.
- In the model a pilot threads ring centres, so it cannot say how often a real person earns "flat out" at I1.
- The platform's AI missed-mouth break-off (`GateRaceController`, the steering provider) tests a ring's whole plane. On an
  I3 apex ring, whose plane runs along the leg into it, drift there may read as a miss and drop the coach's line for that
  ring. The heat ignores crossings past 2.5 mouth radii for this reason (`BrittleStarTimeHeat.IsAnAttempt`). Watch the
  coached AI at the I3 apexes.

### Proposals for Shombith (the kit and its AI; not changed here)

1. **Off Speed, the autopilot should release to Cruise, not Creation.** Today it coasts at half cruise and lays five
   trails at every corner.
2. **Move the coach's corner rule into `BrittleStarSpeedModeAIPolicySO`**, so every mode gains it. This is the road to
   the universal Brittle Star Time genome (Plan step: time-arcade). The coach is a scaffold until then.
3. **`BrittleStarExecutor.SetAutopilotSpeedWish`'s doc says "Only the policy writes it".** In this mode the coach writes
   it.
4. **`BrittleStarTimeCourseTests` reads `speedModeMultiplier.Value`, but the runtime evaluates `Min`.** Both are 6.
   `BrittleStarTimeLineTests` now holds `Min`.

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
(`VesselTransformer.LERP_AMOUNT`), after the mode's own 0.35 s blend (`modeBlendSeconds`), so letting go of Speed Mode
is a coast-down, not a stop. The nearest thing to a brake is Creation Mode (B), x0.5 cruise (22.5 u/s); pressing A again
returns to Cruise (45 u/s).

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
- **The AI cannot roll.** The Brittle Star's autopilot never reads the right stick (`BrittleStarExecutor.AimStick`
  returns zero under the autopilot), so on I4 it cannot use Speed Roll. Off Speed it takes Creation (x0.5), not
  Cruise. Teaching either is a change to the vessel's AI (Shombith's kit), not to this mode; the race coach (section 0)
  works round both with its own corner share and nose follow.
- **The coach is a hand-set scaffold, not the genome.** Its pace tables were searched offline (`--tune`) while Froglet
  Compute is paused, as the AI plan allows by hand (Plan step: time-arcade). The universal Brittle Star Time genome is
  still to be trained on all four rungs through Compute, and replaces the coach's numbers when it is.
- **Variety per seed is orientation only** on I2-I4 (a turn about the pole, a mirror): the shapes' numbers are the
  design, so they do not jitter.
- The Brittle Star's prefab has been stable for months. The two old branches that touched it
  (`align-brittlestar-falcon`, `falcon-brittlestar-reintro`, last pushed 2026-05 and 2026-07) are stale; they change
  vessel files only, none of this mode's.

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
