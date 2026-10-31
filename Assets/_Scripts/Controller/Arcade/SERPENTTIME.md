# Serpentine (Serpent Time) - Technical Documentation

> **Name.** Shown to players as **Serpentine** (Shombith, 2026-10-31). `GameModes.SerpentTime`, the scene, the classes,
> the assets and this file keep the old name: only `DisplayName` and the controller's `ModeName` changed.

> **Status.** Authored headless on 2026-10-27, and reshaped the same day into the **dirty race** (Shombith's brief,
> `Docs/Studios/SERPENT_TIME.md`, prototyped in the Vessel Studio's Serpent page). The course and its tests were
> compiled and RUN outside the editor (400 seeds per intensity); the dirty rules compile against Unity's references.
> **Not run in the Unity editor; nobody has flown it.**

## Overview

`GameModes.SerpentTime = 73` is the **Serpent's Time card**. Until now the Serpent had none
(`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md` §2). It is Thresher Time's shape: a lapped circuit of eight rings on
Headlong's shared solver, flown for two laps, with the first domain home winning (golf).

## The dirty race (`SerpentTimeController`, 2026-10-27)

The brief: "multiple rings in one place ... serpents can spawn walls in front of them so others have to shoot it down
or find another way ... the AI needs to learn to play dirty as well ... to collect sniper bullets they have to wander
to collect more crystals ... the cloak option make it cool". **The vessel kit is never changed by this mode** (Shombith, 2026-10-31: "the vessel kit
as designed always stays the same, we just make the game mode interesting"): the Sniper Shot, the Seed Wall, the cloak
and the pellets do here exactly what they do everywhere. The mode owns the track, the environment and its AI's choices. Its rules:

| Rule | What happens | Where it runs |
|---|---|---|
| **Rings far apart, hairpin bends** (2026-10-31) | Shombith: "no shapes just rings spaced out a lot from each other and hairpin bends". ONE ring a gate (`ringsPerGateByIntensity` {1,1,1,1}), **72 / 60 / 50 / 42 u** at I1-I4 (`SerpentTimeCourse.RingRadiusFor`), on a **switchback ladder** (`SerpentTimeCourse.Build`, replacing Headlong's circle): the lap's 8 rings alternate between two BANKS 76 degrees apart seen from the cell's centre (`BankSpanDegrees`), each bank's rings a short step apart sideways (26 / 23 / 20 / 17 degrees at I1-I4, `LateralStepDegrees`, so the hairpins sharpen with intensity), in a random frame per seed with a little jitter. Every leg crosses the cell (median about 1,000-1,130 u against Headlong's 612), every ring but the ladder's two ends is a hairpin (150-171 degrees, 6 a lap), the two ends close the lap with a sweeping return. Legs keep 1.1x the shell's inner radius from the centre (the nucleus), rings stay inside the shell and two mouths plus 60 u apart: relaxed toward a narrower ladder until they do. A ring faces the leg arriving at it. A Python mirror measured 400 seeds a level before the C#; `SerpentTimeCourseTests` sweeps the same. Ring clusters stay supported, off | Every peer, from the course it was sent |
| **Shapes at the rings** (2026-10-31, REMOVED the same day) | `SerpentineGate` closed each ring with a mode-laid shape (Coil / Maw / Iris: a plain shell, a danger weave with exits, a gold core that disintegrated it, shrinking with intensity), after replacing the flora guards. Removed on Shombith's next look ("remove the obstacles from the middle of the ring no shapes just rings"); the code and its tests are deleted, the lessons are in `/arcadegame` | - |
| **Each intensity a different place** (2026-10-31) | Shombith: "we need the intensity level to feel like different experiences in different environments ... maybe add back fauna" (picked from options: the ladder of places and up to ~25 creatures). Four cell configs, `Assets/_SO_Assets/Cell Configs/Serpentine Cell/Serpentine Cell Config 1-4` (IntensityWise), each Skim Race's barren cell with its own spawn profile and creatures copied from existing configs with only their numbers changed (Skim Race tadpole and brittlestar, Garland shark, Scarab Scramble piranha), live caps summing to at most 25: **I1 Shallows** (tadpoles 6 / max 10), **I2 Grazing Grounds** (tadpoles 8 / 14, brittlestars 3 / 6), **I3 Hunting Grounds** (sharks 2 / 3, piranha 6 / 10, tadpoles 6 / 10), **I4 The Deep** (sharks 2 / 3, piranha 8 / 12, brittlestars 2 / 4, tadpoles 4 / 6). A per-place tint (`CellVisualTint`) was tried and removed the same day (Shombith: "I dont like the tint"); the cell keeps its own colours. Whether the grazers chew the shapes' weave (opening holes, or breaking a core) is whatever their diet does: watch for it | Every peer |
| **The start** (2026-10-31) | Everyone starts on a 90 u ring 300 u behind gate 0, pointed through it (`startLineStandoff`, `startLineRadius`; `IPlayerSpawnLine`, Regatta's and Skein's start). The platform's equatorial ring stood pilots facing the centre with gate 0 ~800 u up on the pole. The course is pure geometry, so the start line builds it early from the same seed (`PickCourseSeed` latches one seed for both, released when the course is raised), the same shell and gate count; it declines to the platform ring when the cell cannot answer yet | The server, during the spawn chain |
| **The cell's current** (2026-10-31) | The track is shown without drawing one (Shombith: "show the track ... not by making a track but something interesting"): the cytoplasm's shards point along the race line instead of at a crystal, through `SnowChanger.IFlow` / `SetFlow` (null for every other mode, which keeps the crystal law). Each shard takes the direction of the nearest leg of the lap (gate to gate, in race order), leaning back toward the line by `currentPull` (0.6) the farther off it is, drawn as a long streak within `currentStreakDistance` (450 u) and shrinking to a mote beyond. A cell with no crystal never built its shards; a flow builds them | Every peer |
| **Gate debris** | 48 loose neutral prisms lie round every gate, 26-120 u beyond its ring; outermost ring (never within 20 u of another gate's rings), the mass a Seed Wall laid at the gate is built from. Every 1.5 s each gate short of 48 loose pieces grows one back; a piece a wall took, a hull broke or a shot cleared stops counting. Laid from `SpawnablePrism` through `PrismTrailBuilder.LayOne` | Every peer, seeded per gate from the course, so the copies match |
| **The AI plays dirty** | Picks the cheapest ring of each gate (distance here + through + on to the next gate, +120 for a walled ring; 60 u hysteresis, held within 250 u unless that ring is walled and its rifle is not ready). A ring is walled when rival prisms cover 40% of its mouth (a guard's plates are not a wall: a Mass or Charge guard closes the ring and is aimed at its heart). Walled ring and the rifle ready (its shipped cooldown is the only limit), 90-1100 u out: scope (LT), settle 0.35 s, fire (RT) once the wall's centre is in the rifle's cone, lower both. Like every racing AI it never detours for a crystal. Just past a ring a rival still needs within 1400 u, it cloaks (80%, 15.5 s apart), so the seed and its wall land in that rival's gate. The platform's timed cloak is filtered off (`AIPilot.SetCycledAbilityFilter`). Every press is the replicated one | The server |

Toasts: `GameToastSituation` 156, the start hint (authored by the generator). 153-155 (rounds, no round, wall hit) were
retired on 2026-10-31 with the round economy and the wall cost, and are not reused.

**Retired 2026-10-31, because each changed the vessel:** sniper rounds bought with crystals (with a press veto,
`R_VesselActionHandler.PressGate`, and a shot event, `SniperShotActionExecutor.Fired`) and a throttle cost on hitting a
rival's Seed Wall (`SerpentWallAssembler.IsWallBrick`). The three vessel files are back to bleeding-edge.

## The loop: fuel

The Serpent's Time ability is Solid Fuel Pellets (`R_VesselActions/SERPENT_FUEL_PELLETS.md`):
- The tank holds four pellets.
- Each press burns one for 3 s.
- Burns overlap and stack. Speed is `50 x (1 + 2n) + 10`: 60 u/s cruising, then 160 / 260 / 360 / 460 u/s with 1-4
  pellets lit.
- One pellet refills every ~3.6 s.

So every straight asks how many pellets it is worth. The Serpent's turn GROWS with speed (`0.4 x speed + 80`
deg/s), so even a full stack turns a ~100 u circle. Corners rarely force it off the boost; the tank does, and the
ladder's real demand is threading a tighter mouth at pellet speed.

## The ladder (`SerpentTimeCourse.ForIntensity`, asserted by `SerpentTimeCourseTests`)

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Mouth radius | 90 | 68 | 52 | 40 |
| Median corners tighter than a full stack's circle (100 u) | 0 | 0 | 2 | 2 |
| Corner floor | 200 | 75 | 55 | 55 |
| Presentation cap | 50° | 80° | 80° | 82° |

All measured over 400 seeds. Every floor sits above 1.5x the cruise circle (33 u), so a Serpent that never burns a
pellet still holds every corner.

## Reused and new

- Everything is the gate-race platform, as for Thresher Time, plus four platform hooks the dirty race added
  (`GateRaceController.ThreadsGate`, `ChooseAIGate`, `AICrystalDetourSlackFor` (unused here since 2026-10-31), `OnLocalNextGateChanged`; every other
  gate race keeps the defaults, which are the old behaviour).
- New:
  - `SerpentTimeCourse`, `SerpentTimeCourseSource`, `SerpentTimeController`, `SerpentTimeCluster` and their tests.
  - Small, opt-in hooks outside the vessel: `AIPilot.SetCycledAbilityFilter`, `RaceGateRing.SetVisualHidden`. No
    vessel file is touched.
  - The scene (a clone of `MinigameThresherTime`), the card, the rule, the preview and the toasts.
  - All the assets come from `Tools/Build/author_serpent_time_assets.py`.
- The controller turns on the Regatta steering (`AICommitFromApproachPoint`).

## Known limitations

- Not run in the editor. The scene needs one open-and-save.
- Every tunable of the dirty race is a serialized field on the controller with its C# default; the committed scene
  does not serialize them yet, so the first save writes the defaults.
- The gate debris is laid on every peer from the same seed, but a gate regrows from what that peer saw destroyed or
  stolen, so after heavy fighting two peers' debris can differ by a few pieces (as their trails can).
- The AI's ring choice and shots are hand-written (round 1). Learning them from play is the automatic sweep's job
  once races are recorded.
- Card art is the placeholder until `/cardart` has a recipe for it.
- The AI's pellet use is the hull's shipped policy and has never been tuned for a race.


## Performance (2026-10-31)

Shombith: "the performance is very very bad, unlike Skim Race". Measured from the owner's captures (`origin/bugledger/owner`,
`prof_MinigameSerpentTime_*_20261029_160748` against `prof_MinigameSkimRace_*_20261025_182141`): player loop 30.5 ms typical
/ 98.8 ms spike against Skim Race's 14.3 / 24.3, and that was in the barren cell, before the Rampage forest. Top self time:
`SerpentWallAssembler.Update` 4.1 ms avg, 36.8 ms max (5 walls), then the GPU wait (3.5 ms against 0.3) and instantiation
(1.7 ms). A growing wall sweeps every prism within its reach every 0.3 s (`QuerySphere`, then a component check each), a
cloak wall grows until its seed breaks, so the cost rises with walls standing and with loose mass near them.

Done in the mode: the forest is gone (back to the Skim Race cell), the ~1,000 cover slabs are gone (one guard plant of 288-360
plates per guarded ring instead), and gates stand 1-2 rings, not 2-3. Markers added for the next capture:
`SerpentTime.Tick`, `SerpentTime.AI`, and inside the wall `SerpentWall.Sweep` / `SteerMembers` / `LayPanels` (diagnostic
only; the wall's behaviour is unchanged). Next: a capture with those markers says which part of the wall to make cheaper.
