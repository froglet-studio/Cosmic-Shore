# Serpent Time - Technical Documentation

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
| **Ring clusters** | Every gate stands as 3 rings (I1, I2) or 2 (I3, I4) side by side in its plane, any one of which threads it (`SerpentTimeCluster`: three rings are half a mouth each, 0.65 mouths out; two are 0.6, 0.7 out; turned 1.7 rad per gate). The course's own ring stays (the objective arrow, the race toasts and the AI's commit read it); only its drawing is hidden (`RaceGateRing.SetVisualHidden`). The local pilot's next cluster lights lime | Every peer, from the course it was sent |
| **Guarded rings** (2026-10-31, replaces the slab cover) | A gate stands 2 rings at I1-I2 and 1 at I3-I4 (`ringsPerGateByIntensity` {2,2,1,1}), and one ring of each gate is guarded by a living Borromean flora (a membrane spanning its own rings), planted at the ring's centre along its axis through the cell's own `CellLifeSpawnerBase.SpawnFlora` (the Spawn Matrix toy's call). Its element walks a per-intensity pattern by gate: I1 Time (plain plates, ram free), I2 Time / Mass (shielded plates bounce a hull), I3 Mass / Charge (every Charge plate is danger), I4 Charge / Mass / Charge. Rampage's four Borromean configs as they ship (Space's 111 u membrane is too wide for a ring and is not in the patterns). Domain: Jade / Ruby / Gold by a hash of the gate (`SerpentTimeCluster.GuardDomain`; never Blue, bait to every school), so every peer plants the same plant; its growth may differ a little per peer, as any unreplicated flora's does. The plant is the cell's: it grows in, fauna graze it, a plant destroyed drops its heart. The AI calls a ring walled when a guard's danger or shielded plates close 40% of the mouth, and shoots it or takes the open ring | Every peer, from the course it was sent |
| **The cell** | The barren Skim Race cell with its trail-grazing food web, Skim Race's own and the fastest arena in the game. Rampage's four forests ran here for a day (2026-10-31) and cost too much frame time (up to ~60k prisms, every one in reach of a growing Seed Wall's sweep) | The Cell |
| **Gate debris** | 48 loose neutral prisms lie round every gate, 26-120 u beyond its outermost ring (never within 20 u of another gate's rings), the mass a Seed Wall laid at the gate is built from. Every 1.5 s each gate short of 48 loose pieces grows one back; a piece a wall took, a hull broke or a shot cleared stops counting. Laid from `SpawnablePrism` through `PrismTrailBuilder.LayOne` | Every peer, seeded per gate from the course, so the copies match |
| **The AI plays dirty** | Picks the cheapest ring of each gate (distance here + through + on to the next gate, +120 for a walled ring; 60 u hysteresis, held within 250 u unless that ring is walled and its rifle is not ready). A ring is walled when rival prisms cover 40% of its mouth. Walled ring and the rifle ready (its shipped cooldown is the only limit), 90-1100 u out: scope (LT), settle 0.35 s, fire (RT) once the wall's centre is in the rifle's cone, lower both. Like every racing AI it never detours for a crystal. Just past a ring a rival still needs within 1400 u, it cloaks (80%, 15.5 s apart), so the seed and its wall land in that rival's gate. The platform's timed cloak is filtered off (`AIPilot.SetCycledAbilityFilter`). Every press is the replicated one | The server |

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
