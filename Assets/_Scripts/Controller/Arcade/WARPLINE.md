# Warpline (`GameModes.Warpline = 65`)

The **Stoat's time race**. A closed loop of **five** switch rings is cut through the barren race cell;
every pilot flies **two laps** of it in order, and the first **DOMAIN** whose **LEAD RUNNER** threads
the last ring of the last lap wins (golf: finish time).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameWarpline.unity` (cloned from `MinigameSlingshot`) |
| Controller | `WarplineController : GateRaceController` |
| Course | `SlingshotCourse.ForIntensity` → the shared `HeadlongCircuit.Generate`, target/laps = 5 rings |
| Metric | `ScoringMetric.SwitchesThreaded` (9), **reused**, golf |
| Target | `EndConditionOverridesSO.warplineGateTarget` = **10** (2 laps × 5 rings) — FrogletTools ▸ Game Modes ▸ End Game Conditions |
| Card | `_SO_Assets/Games/ArcadeGameWarpline.asset` — Vessels: `SO_Class_Stoat` only |
| Generator | `Tools/Build/author_warpline_assets.py` (`--check`) |
| Vessel | `R_VesselActions/STOAT_DIPOLE.md` (the round-15 field dipole + pathfinder) |

## 1. What the mode is asking

**Where to put the poles.** The Stoat cruises at 60 u/s and turns at only 48 °/s on its own (the
round-15 ×0.4): its triggers hold one sink–source pair open 250 u ahead, the difference pulling the
poles apart sideways and the sum lengthways, and its **pathfinder** draws the line it will fly. While
the poles **warp** that line — bend it 3° or more, or send it through the wormhole — it turns lime and
the hull flies down it faster: ×2 at rest, ×3 at Time 5, ×4 at Time 10. The line is the line; only
the time it takes changes. That is the race: **every second you are not warped, you are flying at
cruise.**

So each leg is a decision about the pole you pull:

- **Straight at the next ring** — both triggers: the sink dead ahead, the source beyond it. Your line
  runs through the sink, out of the source 120 u further on, warped all the way.
- **Round a corner** — the trigger on that side: the sink swings out to the side and the field bends
  your line into the turn (a turn the hull can no longer make alone at speed). Bend it too far and
  the line closes on itself — still warped, but now you are flying a circle.
- **Neither** — cruise.

Five rings a lap on Slingshot's 600 u circle, so each leg is ~750 u: long enough to lay a pair, let it
grow (0.9 s) and ride it, and short enough that the next corner is always being set up.

What the sink does to everything else applies here too: a rival who flies into your sink comes out
of your source **stripped of its crystals**, left on your side of the sink; prisms it swallows vanish
(or, with the Space upgrade, come out yours); creatures are swallowed. The barren race cell carries
little of any of them, so in this mode they are hazards rather than the point.

## 2. Reused, not built

Everything but the cut is the platform's, exactly as Slingshot: `GateRaceController` (rings,
ordering, laps, the lead-runner rule, the AI approach), `RaceGateTurnMonitor`,
`RaceGateObjectiveProvider` (registered in `MiniGameHUD`), `GateRaceScoringRuleSO` (one more asset),
the barren race cell, the equatorial spawn ring, the shared race toasts (`DomainRaceToasts`), the
comeback (`ComebackRatePerScoreDeficit` 0.45: a quarter-of-race deficit, 2.5 gates, buys 1.1 element
levels — and Time sets the warp boost, so it lands on the mode's axis).

## 3. AI

**2026-10-10, ported from the Stoat Flight Studio (`/vessel-studio` D24):**
- **AI difficulty.** The card now offers the lobby's Easy / Medium / Hard picker (`AIDifficultyRules.IsOfferedFor`).
  `GateRaceController.ArmRacers` races Easy and Medium on `GateRaceHandicap`, the Skim Race's two mistakes applied to
  rings, with the same numbers (`SkimRaceDifficultySO`): a ring noticed 0.5 / 0.25 s late (the AI flies straight on
  until then), and 9.9% / 4.5% of rings misjudged (believed two ring-radii off to one side, flown past, turned back
  for). Hard races the true course.
- **The path-watching autopilot** (`StoatDipoleConfig` ▸ *Autopilot - watching the path*, on by default). The AI holds
  its pair while the pathfinder says the path is WARPED, lets go as its target comes within 60 u or once the warp
  has been off for 0.5 s, and lays the next pair 0.25 s later. In the studio this took Hard from 70-89 s to 49-59 s
  over the four courses. Turn it off for the timed hold described below.

An AI Stoat flies the platform's gate approach (commit 160 / lead 180 / through 120 u) and **lays
pairs**: `StoatDipoleExecutor.Autopilot` pulls both triggers when its target is within 12° of the
nose and at least 300 u away (the wormhole line, warped), or the trigger on the target's side when
it is up to 75° off (the field turns it in), holds for 4 s at a 0.8 squeeze and lets go, at most
every 2 s — both edges through the **replicated** press path. It is a turning and boosting aid, not a
racing line; a human reading the pathfinder beats it.

## 4. Known limit — two pairs at a time

The black-hole budget is four holes (`BlackHolePhysics.NativeWells.Capacity`, the lens's well bank), so
at most **two** Stoats hold a pair at once; a third pilot's squeeze lays nothing until one closes
(`STOAT_DIPOLE.md` §3). A 2-Stoat race is unaffected; at 3–4 Stoats the pairs are first come, first
served. Raising the budget is a lens/shader change.

## 5. Status

Authored by the generator; compiled headless (`Tools/Build/unity_refcompile`), **not yet run in the
Editor** — `/verify-unity` was not available in the session that wrote it
(`Docs/UNITY_VERIFICATION_CHECKLIST.md`). The card background is the shared placeholder until
`/cardart` renders the circuit.
