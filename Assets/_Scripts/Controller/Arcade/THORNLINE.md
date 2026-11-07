# Thornline - the Shrike's Time race

> **Status.** Authored headless on 2026-11-07 (`claude/shrike-sling-race`), for Garrett's ask that every vessel get a
> Time game that suits it. The course and its tests were compiled and RUN outside the editor (400 seeds per intensity,
> `Tools/Build/thornline_course_harness/run.sh`), and the mode compiles against Unity's reference assemblies.
> **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.Thornline = 79` is the **Shrike's Time card**. (78 was SpeedPainter's for one commit on its own branch
before it became LanternRun 75; it is left alone.)

## 1. Why this race, for this hull

The Shrike cruises at **18 u/s**. Everything past a crawl is its **Void Sling** (`R_VesselActions/SHRIKE.md` §3): in
Time mode RT tethers to the first block on the weapons' axis within **1,600 u** and the hull flies there in **one
second at any distance**, docking on the block. The sling then reloads: **6 s** at Time 0, **2.5 s** at Time 10, and
the clock runs **x1.6** while docked on a plain block and **x2.5** on super-shielded mass or in a nest. A crystal
resets it. No anchor, no sling, and no cooldown spent.

So a Shrike race is not about distance at all. It is about **aim** (a ray with no radius, against one block) and the
**reload**. Thornline is built from exactly those:

- a chain of **thorns**, one big block each (one block, so the thorn you sit on never catches your next ray);
- a **ring at the middle of every leg**, so threading the ring IS hitting the thorn behind it;
- the reload is the clock: a leg costs `1 s + (cooldown - 1 s) / dock bonus`, whatever its length.

The vessel is untouched (the LOCKED mode rule): the sling, the dock bonus, the lance that can break a plain thorn,
the nests and the crystal reset are the shipped kit. The mode's levers are the thorns, the crystals and the AI's aim.

## 2. The ladder (`ThornlineCourse.ForIntensity`, held by `ThornlineCourseTests`)

| | I1: the sling | I2: the reload | I3: the snipe | I4: the larder |
|---|---|---|---|---|
| New thing | dock, aim, sling | plain thorns (x1.6) and Time crystals on the rim lines | three snipes through two rings | every thorn plain, smallest, farthest |
| Thorn (cube side) | 56 u | 46 u | 40 u | 32 u |
| Ring radius | 64 | 56 | 50 | 44 |
| Legs | 550-750 | 600-850 | 650-950 | 750-1150 |
| Bends | 35-50° | 30-60° | 30-75° | 35-95° |
| Thorns | all super-shielded | every 2nd plain | every 2nd plain | all plain |
| Crystals / snipes | 0 / 0 | 3 / 0 | 3 / 3 | 3 / 3 |

Sixteen rings at every level (the end condition, one count), seventeen thorns: the **start thorn** sits on the spawn
ring's pole, 1.5x the size and always super-shielded, so every pilot's first sling is the same shot from the same
distance.

**The rim crystal (I2+).** A Time crystal sits 80% down a leg, on the line from the leg's start to a point three
quarters of the way out on the thorn's face. Aim at the thorn's centre and you pass 13-17 u wide of it; aim at the
rim and you fly through it, and the sling is ready the moment you dock. The crystal is also clear of every other
leg's centre line, so only the aimed rim takes it.

**The snipe (I3+).** At three bends the thorn after next lines up through **both** rings (each passed at least 12 u
inside its mouth, the near thorn cleared by its half-diagonal plus 23 u), 1,150-1,450 u away. Hit it and you skip a
whole reload; miss and the press does nothing (no anchor, no cooldown), so the cost of trying is only the time spent
aiming. Every OTHER pair of thorns in sling range misses at least one ring between them by 15 u, so no ring is ever
skipped by accident.

### What it measures (offline, 400 seeds, `run.sh --sweep`)

Race clock at Time 0 for a pilot who slings the moment the reload is up (aim time NOT counted):

| | dock every thorn | take the snipes | snipes + rim crystals |
|---|---|---|---|
| I1 | 49.0 s | - | - |
| I2 | 58.0 s | - | 48.1 s |
| I3 | 58.0 s | 49.0 s | 41.0 s |
| I4 | 65.9 s | 53.5 s | 42.2 s |

Aim adds roughly a second a leg, so a first race runs ~65-85 s. A pilot who reads the course beats one who docks on
every thorn by 17% (I2), 29% (I3) and 36% (I4) on the clock alone: the skill is the shot, not the stick.

## 3. Every peer lays the same course from the broadcast rings

Only the rings are broadcast (the gate-race platform's `SyncCourse_ClientRpc`). Because each ring sits at the middle
of its leg, a thorn is where two consecutive rings' lines meet, and the two end thorns follow from the middle
(`ThornlineCourse.ThornsFor`; the tests rebuild every chain from moved rings within 0.05 u). The kinds follow the
index, the crystals follow the thorns. Nothing new is replicated.

A thorn broken by a lance (plain thorns only, below Void Pierce) is laid again 3 s later once no hull is next to it;
a taken crystal grows back after 5 s. Rival sling trails lie on the racing line and catch a follower's sling (it docks
on the first block it touches): fly a parallel line across the thorn's face, or lance the trail. That interaction is
the kit's, not the mode's, and is left alone.

## 4. AI

`ThornlineController.TryOverrideAim` points the AI at a **thorn**, never a ring, because the Shrike's autopilot slings
when the weapons' axis finds a block (`ShrikeExecutor.Autopilot`: within 20° of its target, at 300 u or more). In
order: the thorn after next when the line from here threads both rings and clears the near thorn (a snipe); the
thorn behind the next ring when the line threads it; otherwise the thorn the ring's leg starts from (the start
thorn at the gun, or the thorn a short sling fell short of). Not verified in the editor: whether `AIPilot`'s aim is
tight enough to put a zero-radius ray on a 32 u block at 1,000 u (2°). If it is not, the AI will crawl; the fix is
in the AI's aim, not in bigger thorns.

Two autopilot habits a race may show (both the vessel's, reported, not changed here):
- it presses Mass after 3 s docked (`aiDockSeconds`), which detaches before a plain thorn's 3.1 s reload is up;
- it lances when two or more rival blocks are on its line (`aiLanceMinTargets` 2), which clears rival trails but
  never a thorn (one block).

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (rings) | FrogletTools > Game Modes > End Game Conditions, "Thornline - Gate Target" (`thornlineGateTarget`) | **16** |
| the ladder | `ThornlineCourse.ForIntensity` | §2 |
| Shrike numbers the cut depends on | `ThornlineCourse` constants, read back off `ShrikeConfig.asset` by `Course_constants_match_the_shipped_Shrike` | 1600 / 1 / 6->2.5 / 1.6 / 2.5 / 3 |
| thorn and crystal regrow | scene > `ThornlineController` | 3 s / 5 s |
| AI snipes | scene > `aiTakesSnipes` | on |
| detection clamp | scene > `maxPlausibleSpeed` | **4000** (a 1,600 u sling at 60 fps, twice) |
| comeback rate | `ArcadeGameThornline.asset` | **0.35** (4 rings behind buys 1.4 levels; Time shortens the reload) |

## 6. Assets

All authored by `Tools/Build/author_thornline_assets.py` (on `hull_time_race.py`, Thresher Time's scene as the donor):
the card, the scoring rule (`GateRaceScoringRuleSO`, metric 9), the scene, the preview, the toasts, the registrations.
The card background is the `/cardart` COURSE render (`CardBackgrounds/Thornline.png`, the `thornline` layer in the
card-art harness: rings, path and thorns at intensity 2). `--check` is the drift gate.

## 7. In-editor verification (owed)

1. Open the scene once and save it (it is a text clone); then `author_thornline_assets.py --check`.
2. I1, one Shrike: the 17 thorns stand where the rings' lines meet; sling to the start thorn, then thorn to thorn;
   every ring counts.
3. A thorn of 56 u (I1) and 32 u (I4) is visible and hittable with the eyepiece from 1,000 u.
4. I2: plain thorns reload visibly slower; a rim shot takes the crystal and the next sling is ready on docking.
5. I3: a snipe threads both rings and counts both.
6. Lance a plain thorn: it comes back 3 s after you leave.
7. Two AI Shrikes: they sling thorn to thorn and finish. If they crawl, note the aim error (§4).
8. MPPM two clients: both see the same thorns and crystals.

## 8. Known limitations

- **Aim time is not modelled.** The clock above is the reload only.
- **The start.** Pilots spawn on the equatorial ring and the start thorn sits on the pole; the first sling is the
  same shot for everyone, but its distance depends on the spawn ring's radius (under 1,600 u in the shipped scenes is
  assumed, not measured).
- **No offline race model for self-play** beyond the clock in `run.sh --sweep`; the AI learns from recorded races.
