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
to collect more crystals ... the cloak option make it cool". Four rules, all in the controller:

| Rule | What happens | Where it runs |
|---|---|---|
| **Ring clusters** | Every gate stands as 3 rings (I1, I2) or 2 (I3, I4) side by side in its plane, any one of which threads it (`SerpentTimeCluster`: three rings are half a mouth each, 0.65 mouths out; two are 0.6, 0.7 out; turned 1.7 rad per gate). The course's own ring stays (the objective arrow, the race toasts and the AI's commit read it); only its drawing is hidden (`RaceGateRing.SetVisualHidden`). The local pilot's next cluster lights lime | Every peer, from the course it was sent |
| **Seed Walls cost speed** | A hull whose path (plus a lead past its nose) passes a RIVAL's Seed Wall brick or seed (`SerpentWallAssembler.IsWallBrick`) drops to 0.5 throttle (bare brick) or 0.15 (shielded), recovering over 1.2 s; 0.5 s between hits. Your domain's walls let you through. The Serpent's own prism impact damages the brick and never slowed the hull, so the cost is this mode's | The machine that flies the hull |
| **Gate debris** | 48 loose neutral prisms lie round every gate, 26-120 u beyond its outermost ring (never within 20 u of another gate's rings), the mass a Seed Wall laid at the gate is built from. Every 1.5 s each gate short of 48 loose pieces grows one back; a piece a wall took, a hull broke or a shot cleared stops counting. Laid from `SpawnablePrism` through `PrismTrailBuilder.LayOne` | Every peer, seeded per gate from the course, so the copies match |
| **Rounds cost crystals** | Start with 1 round, +1 per crystal (`ElementalCrystalImpactor.OnCrystalCollected`), at most 3; a shot spends one (`SniperShotActionExecutor.Fired`, raised on the authoritative machine only). RT while scoped with no round is dropped (`R_VesselActionHandler.PressGate`); unscoped RT is still the cloak | The machine that flies the hull (the same machine whose rifle cooldown is authoritative), so nothing new is replicated |
| **The AI plays dirty** | Picks the cheapest ring of each gate (distance here + through + on to the next gate, +120 for a walled ring while it holds a round, +600 without; 60 u hysteresis, held within 250 u). A ring is walled when rival bricks cover 40% of its mouth. Walled ring + a round, 90-1100 u out: scope (LT), settle 0.35 s, fire (RT) once the wall's centre is in the rifle's cone, lower both. Short of rounds it detours for crystals (900 u of extra flying with none, 250 below the cap). Just past a ring a rival still needs within 1400 u, it cloaks (80%, 15.5 s apart), so the seed and its wall land in that rival's gate. The platform's timed cloak is filtered off (`AIPilot.SetCycledAbilityFilter`). Every press is the replicated one | The server |

Toasts (`GameToastSituation` 153-156, authored by the generator): rounds held, no round, wall hit, the start hint.

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
  (`GateRaceController.ThreadsGate`, `ChooseAIGate`, `AICrystalDetourSlackFor`, `OnLocalNextGateChanged`; every other
  gate race keeps the defaults, which are the old behaviour).
- New:
  - `SerpentTimeCourse`, `SerpentTimeCourseSource`, `SerpentTimeController`, `SerpentTimeCluster` and their tests.
  - Small, opt-in hooks elsewhere: `R_VesselActionHandler.PressGate`, `SniperShotActionExecutor.Fired`,
    `SerpentWallAssembler.IsWallBrick`, `AIPilot.SetCycledAbilityFilter`, `RaceGateRing.SetVisualHidden`.
  - The scene (a clone of `MinigameThresherTime`), the card, the rule, the preview and the toasts.
  - All the assets come from `Tools/Build/author_serpent_time_assets.py`.
- The controller turns on the Regatta steering (`AICommitFromApproachPoint`).

## Known limitations

- Not run in the editor. The scene needs one open-and-save.
- Every tunable of the dirty race is a serialized field on the controller with its C# default; the committed scene
  does not serialize them yet, so the first save writes the defaults.
- The gate debris is laid on every peer from the same seed, but a gate regrows from what that peer saw destroyed or
  stolen, so after heavy fighting two peers' debris can differ by a few pieces (as their trails can).
- A racing AI flies the waypoints, never a crystal (Yash, 2026-10-26) - except here, because a round IS a crystal;
  an AI holding three rounds takes none.
- The AI's ring choice and shots are hand-written (round 1). Learning them from play is the automatic sweep's job
  once races are recorded.
- Card art is the placeholder until `/cardart` has a recipe for it.
- The AI's pellet use is the hull's shipped policy and has never been tuned for a race.
