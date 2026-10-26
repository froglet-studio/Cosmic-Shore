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
the hull flies down it faster: ×2.5 at rest, ×3.75 at Time 5, ×5 at Time 10. The line is the line; only
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

**Both modes since 2026-10-13** (`R_VesselActions/STOAT_DIPOLE.md` §1a): the autopilot also switches the Stoat's MODE — `StoatModeExecutor` presses B for it, through the replicated path, to fly Time mode's orbit when its target is ≥ 70° off the nose (the orbit is the tighter turn) and back to Mass mode, and the dipole above, once the target is within 35°; at most every 3 s, never mid-squeeze (`StoatModeAction` § Autopilot). In Time mode it slings with `StoatSlingExecutor.AutopilotSling`.

### 3.1 Retrained from Yash's races (2026-10-26)

Yash: "re train Stoat", which lifts the hold the Stoat's AI had been on since 2026-10-17 (issue 1198).

**What his 11 races (I1-I3) showed.** The 22 AI Stoat rivals finished none of them: 2.6 rings a minute, stalled on ring
0, against Yash's 79-121 s. Raced offline on those courses (`Tools/Build/switchback_dolphin_sim.py --mode Warpline`,
140 u/s and 48 deg/s, no warp), the shipped gate approach finishes 0 of 11. The approach point sat 180 u out against a
160 u commit, and on its 167 u turning circle the Stoat circled the 32-48 u mouths. That is the defect Switchback,
Breakwater and Waystation had.

**What changed:**
- `WarplineController.AICommitFromApproachPoint` = true, the Regatta steering. Offline: 11 of 11 finished, in 136-255 s
  (no warp modelled, so slower than a real Stoat). Test: `SwitchbackAISteeringTests.Warpline_*`.
- `StoatDipoleConfig_Warpline` and `StoatSlingConfig_Warpline` (`Resources/AIBoostPolicies`, AIModeTuning loads them
  in Warpline only), learned from his three fastest races (`Docs/AI_SYSTEM/LEARNED_DRIVES_Warpline.md`). Pairs: from
  206 u of the ring (was 300), every 1.5 s (2), held 0.9-2.1 s (0.8-15), lapping the sink at most 78 deg (360). Slings:
  from 103 u (120), every 1.2 s (3), for a turn from 7 deg (30), released within 14 deg (20), at a full squeeze
  (0.5).
- `StoatDipoleConfig.asset` now serializes `autopilotMaxOrbitDegrees` at its code default (360, no change in play), so
  the learned 78 deg had a field to land in. The learner now reports a learned number whose field the asset lacks,
  instead of dropping it.

The Stoat's own configs, which every other mode flies, are unchanged.

### 3.2 The warp lets go 250 u out (2026-10-27)

With the steering fixed, the AI still missed 4-7 mouths a race and took about twice Yash's time (offline on his 15
races, I1-I4): warped to ~140 u/s on a 167 u turning circle, it cannot line up on a 26-48 u ring from the base
autopilot's 60 u let-go. With the warp modelled (`switchback_dolphin_sim.py --mode Warpline`: laid at 1.5x the let-go,
let go at it, held 1.0-2.36 s), letting go 250 u out finishes all 15 courses in 82-92 s with no missed mouth, at warp
speeds of 120-170 u/s (Yash: median 92 s); 175 u starts missing again. So `StoatDipoleConfig_Warpline.autopilotLetGoNear`
is 250, set by hand: Yash threads half his rings still warped, steering by the pair's bend, which the AI does not use,
so his ~50 u let-go is not one the AI can copy. The learner never writes that field (`MINIGAME_TRAINING_PLAN.md` §7).

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
