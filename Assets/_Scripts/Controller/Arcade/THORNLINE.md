# Thornline - the Shrike's Time race

> **Status.** Authored headless on 2026-11-07 (`claude/shrike-sling-race`), for Garrett's ask that every vessel get a
> Time game that suits it. The course and its tests were compiled and RUN outside the editor (400 seeds per intensity,
> `Tools/Build/thornline_course_harness/run.sh`), and the mode compiles against Unity's reference assemblies.
> **Nothing here has been run in the Unity editor, and nobody has flown it.**
>
> **Upgraded 2026-11-17, "the honest line"** (round 5, Docs/Thornline/UPGRADE_SPEC.md): the sling catches a block only
> within 12 u of its CENTRE, so the I2/I3 rim crystal anchored on nothing and the AI (no nose lead, every difficulty
> alike) could not race. Now the rim mark sits inside the 12 u, every seat starts one sling from the start thorn, a
> sight shows the part of the thorn that catches, rivals' thorns glow in their colour, toasts 420-426 call the beats,
> and the AI aims at lanes with a nose lead by difficulty: in the offline replica (`run.sh --ai`) every difficulty
> finishes every rung, solo and in a pack, in order. Fun 5.7 -> 6.7 est. Still nothing run in Unity (§7).

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

**What the sling really catches.** `ShrikeExecutor.FindSlingAnchor` takes a block only when its CENTRE lies within
`dockQueryReach` **12 u** of the ray (then the ray must enter its box). On a 32-84 u thorn that is a 12 u disc round the
centre, not the drawn face: aim at a thorn's edge and the press does nothing. The mode is cut for it: the sight (§5)
shows the disc, the rim mark sits inside it and the AI's lanes are 6.5 u off the centre. (Proposal to Garrett: the
field's own tooltip says it must cover the largest block's half size; UPGRADE_SPEC.md §6.)

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

**The rim crystal (I2+).** A Time crystal sits 90% down a leg, on the line from the leg's start to the RIM MARK, 8 u
off the thorn's centre (inside the 12 u the sling catches). Aim at the thorn's centre and you pass 7.2 u wide of it;
aim through it and you anchor, and the sling is ready the moment you dock. 0.9 (not 0.8) because a pilot docks on the
last thorn's FACE, up to ~30 u off its centre, and the line from there through the crystal lands 1/9 of that off the
mark: still inside 12 u. The crystal is clear of every other leg's lanes. (Until 2026-11-17 the mark was 0.375 x the
thorn's side off centre, 17 / 15 u at I2 / I3, outside the 12 u: the rim shot anchored on nothing.)

**The snipe (I3+).** At three bends the thorn after next lines up through **both** rings (each passed at least 12 u
inside its mouth, the near thorn cleared by its half-diagonal plus 23 u), 1,150-1,450 u away. Hit it and you skip a
whole reload; miss and the press does nothing (no anchor, no cooldown), so the cost of trying is only the time spent
aiming. Every OTHER pair of thorns in sling range misses at least one ring between them by 15 u, so no ring is ever
skipped by accident.

### What it measures (offline)

The AI race replica (`run.sh --ai --seeds 100`, aim modelled; RaceReplica.cs): median race, seconds, solo / a pack
of four. Every seat finishes every rung (100/100).

| | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| I1 | 64.5 / 68.4 | 56.4 / 59.0 | 49.3 / 49.5 | 49.3 / 49.4 |
| I2 | 72.0 / 89.6 | 62.7 / 82.2 | 57.8 / 70.0 | 52.7 / 58.1 |
| I3 | 72.8 / 87.3 | 63.0 / 80.6 | 54.6 / 59.5 | 49.3 / 54.5 |
| I4 | 88.2 / 103.0 | 73.4 / 94.2 | 57.9 / 70.0 | 53.5 / 59.2 |

Reading the course with the aim modelled (`run.sh --read`, Legendary's aim): snipes and crystals save **9 / 15 / 19 %**
at I2-I4 over docking every thorn. Against the best FIXED habit the saving is 0 % by construction (a seen snipe or
crystal is never a loss); the skill is the shot.

The race clock alone (400 seeds, `run.sh --sweep`), at Time 0 for a pilot who slings the moment the reload is up (aim
time NOT counted):

| | dock every thorn | take the snipes | snipes + rim crystals |
|---|---|---|---|
| I1 | 49.0 s | - | - |
| I2 | 58.0 s | - | 48.1 s |
| I3 | 58.0 s | 49.0 s | 41.0 s |
| I4 | 65.9 s | 53.5 s | 42.2 s |

The clock-only saving (17 / 29 / 36 %) counts no aim; the replica's 9 / 15 / 19 % above is the honest one.

## 3. Every peer lays the same course from the broadcast rings

**The start (2026-11-17).** `ThornlineController` is an `IPlayerSpawnLine`: every seat sits 640 u from the start
thorn, 30 deg below it, evenly round the pole and facing it (`ThornlineCourse.StartPoses`, seed-free), so the first
sling is the same shot for everyone at the gun, inside sling range and the AI's 300 u minimum. The walk keeps every
later thorn and ring off the seat cone (`Fits`, `SeatsClear`; 128 attempts, 0/400 failures at every rung).

Only the rings are broadcast (the gate-race platform's `SyncCourse_ClientRpc`). Because each ring sits at the middle
of its leg, a thorn is where two consecutive rings' lines meet, and the two end thorns follow from the middle
(`ThornlineCourse.ThornsFor`; the tests rebuild every chain from moved rings within 0.05 u). The kinds follow the
index, the crystals follow the thorns. Nothing new is replicated.

A thorn broken by a lance (plain thorns only, below Void Pierce) is laid again 3 s later once no hull is INSIDE its
box (`ThornlineCourse.RegrowBlocked`; a sphere of size + 30 u until 2026-11-17, which a hull hanging at the face held
off), as a live ribbon so it never holds the arena-ready gate; a taken crystal grows back after 5 s. Rival sling trails lie on the racing line and catch a follower's sling (it docks
on the first block it touches): fly a parallel line across the thorn's face, or lance the trail. That interaction is
the kit's, not the mode's, and is left alone.

## 4. AI (2026-11-17: ThornlineAIPlan, ThornlineController.AI.cs)

The AI is input only: `TryOverrideAim` returns a point and the Shrike's own autopilot slings when its axis anchors on
a block within 20 deg of it (`ShrikeExecutor.Autopilot`, 300 u or more away). The point is `ThornlineAIPlan.Choose`,
the same code the harness races offline:
- never a thorn's centre: a **lane** 6.5 u off it (8 per thorn, spread by seat), inside the 12 u the sling catches and
  outside the 3.5 u the autopilot counts toward its lance at Charge 0, so the course never pulls an AI out of Time
  mode and two AIs fly different lines;
- in order: a **snipe** through both rings (Hard and Legendary), a **shot through a live crystal** (Legendary), the
  next thorn, the leg's start thorn when the next is not on a ring's line, and after 15 s without a ring the nearest
  live thorn in range for 6 s (a safety net the replica never needs; its control does not fire);
- a broken thorn is never a target.

**Aim by difficulty.** `OnRacerArmed` sets each AI's nose lead (`AIPilot.NoseLeadGain`) by its seat's difficulty:
**2.5 / 3 / 8 / 16**, capped 10 deg past the aim, at the fleet nose lag. Without it the stick's cubic law closes the
last degree onto a 12 u disc at 1,000 u so slowly that 14 of 20 I1 races finish (median 216 s). Difficulty is aim and
reading, never a delay: the autopilot detaches at 3 s docked the moment it is reloaded (the 2026-11-07 fix,
`d92bfa36e1`, verified in the code), and a plain thorn's reload is 3.1 s, so any wait after the reload leaves every
plain thorn and aims at a third of the turn rate (measured; UPGRADE_SPEC.md §5).

Habits of the kit the replica does not model (owed to the editor): Charge-mode paint at a rival in the 8 deg cone once
a crystal has grown a missile flora; the nest a detach leaves; what a hull drifting into a thorn does.

### 4.1 The Legendary row, trained in the real game (2026-11-17, elemental loop step 3)

Shrike run1 used `Tools/Compute/evaluators/time_ai_search.py --hull Shrike`: the race judge, Amoebius headless, with the
shipped C# and nothing modelled. The search moved only the Legendary row of `aiNoseLeadByDifficulty` and
`aiNoseFollowByDifficulty`, through the controller, so nothing on the vessel changed.

- **Search:** seeds 11-14, two constrained generations and one Pareto generation, popsize 4. The conditions were I1, I2
  and I4, I1 at Time 10, I4 from Time 5, I4 from rest, and two wide courses.
- **Confirm:** fresh seeds 101-104, 32 flights a side, and none unfinished. Loss went from 2.67 to 2.39 against 16 and
  the fleet lag.

| Legendary, seeds 101-104, mean s | I1 | I2 | I4 | I1 @T10 | I4 @T5 | I4 @T0 | W0 | W1 |
|---|---|---|---|---|---|---|---|---|
| before (lead 16, fleet lag) | 48.8 | 38.8 | 40.7 | 33.0 | 49.0 | 62.5 | 76.9 | 39.6 |
| after (lead 9.2, follow 1.6/s) | 48.9 | 39.9 | 41.6 | 31.9 | 42.2 | 66.3 | 43.5 | 44.1 |

Most of the gain is in the races that went wrong before: the 77 s wide course and the I4 start from Time 5. The card's
own rungs stay within about 1 s. A crystal detour gene of 55 u scored a little better again (2.20), but it is not
shipped: there is no `RaceAICrystalGenesSO` asset for it to live in yet, and its edge is within the noise. The files are
in `/mnt/project-files/regatta-flywheel/train/Shrike/run1/`.

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (rings) | FrogletTools > Game Modes > End Game Conditions, "Thornline - Gate Target" (`thornlineGateTarget`) | **16** |
| the ladder | `ThornlineCourse.ForIntensity` | §2 |
| Shrike numbers the cut depends on | `ThornlineCourse` constants, read back off `ShrikeConfig.asset` by `Course_constants_match_the_shipped_Shrike` | 1600 / 1 / 6->2.5 / 1.6 / 2.5 / 3 |
| thorn and crystal regrow | scene > `ThornlineController` | 3 s / 5 s |
| AI snipes | scene > `aiTakesSnipes` (Hard and up) | on |
| AI aim | scene > `aiNoseLeadByDifficulty` / `aiNoseFollowByDifficulty` / `aiNoseLeadMaxExtraDegrees` (= `ThornlineAIPlan`) | 2.5, 3, 8, 16 / fleet / 10 |
| the AI's lanes, rim mark, seats | `ThornlineCourse` (`LaneOffset`, `CrystalRimOffset`, `CrystalAlong`, `StartSeatDistance`, `StartSeatDepression`) | 6.5 u, 8 u, 0.9, 640 u, 30 deg |
| toasts (local pilot) | scene > `hintToast`..`overtakenToast`; text on `GameToastConfig_Thornline` | 420-426 |
| sounds | scene > nine `...Event` slots | **empty** |
| the sight, rival glow | scene > `sightReloadingScale`, `rivalThornGlow` | 0.35, 0.35 |
| detection clamp | scene > `maxPlausibleSpeed` | **4000** (a 1,600 u sling at 60 fps, twice) |
| comeback rate | `ArcadeGameThornline.asset` | **0.35** (4 rings behind buys 1.4 levels; Time shortens the reload) |

## 6. Assets

All authored by `Tools/Build/author_thornline_assets.py` (on `hull_time_race.py`, Thresher Time's scene as the donor):
the card, the scoring rule (`GateRaceScoringRuleSO`, metric 9), the scene, the preview, the toasts, the registrations.
The card background is the `/cardart` COURSE render (`CardBackgrounds/Thornline.png`, the `thornline` layer in the
card-art harness: rings, path and thorns at intensity 2). `--check` is the drift gate.

## 7. In-editor verification (owed)

1. Open the scene once and save it (it is a text clone); then `author_thornline_assets.py --check`.
2. I1, one Shrike: the 17 thorns stand where the rings' lines meet; the pilots spawn on the seat cone facing the start
   thorn and the first sling reaches it; thorn to thorn; every ring counts.
3. The sight: a dome on the next thorn's face, grey and growing while the sling reloads, Lime when ready; a press
   inside it anchors from 1,000 u at I4 (32 u thorns), a press on the drawn edge outside it does not.
4. I2: plain thorns reload visibly slower; a shot through a rim crystal takes it at sling speed (the 25 Hz physics
   tunnelling risk) and the next sling is ready on docking; toast 422.
5. I3: a snipe threads both rings and counts both; toast 421.
6. Lance a plain thorn: it comes back 3 s after you leave its box, even with you hanging where its face was.
7. Four AI Shrikes, one per difficulty, at I1 and I4: all finish, Legendary first, Easy last. Note seconds spent in
   Space mode (lancing trails) and Charge mode (painting a rival), and any detach that strands an AI.
8. A rival docked on a thorn makes it glow faintly in their colour; passing and being passed calls 425 / 426.
9. MPPM two clients: both see the same thorns, crystals and seats; the sight and glow are local.

## 8. Known limitations

- **The AI replica is a model.** It copies AIPilot's stick law and the Shrike's rules (the self-test greps their
  numbers) but not AIPilot's orbit break-off, Charge diversions, nests, latency or collider contact.
- **No learner reads the Shrike** (its prefab carries `SparrowAIBoostPolicy`): the AI's rows are the replica's, not
  learned from recorded races.
