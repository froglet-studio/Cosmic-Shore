# AI pilot system — roadmap

**Status: PLAN (2026-09-28).** This doc covers:
- how the design in [`ARCHITECTURE.md`](ARCHITECTURE.md) gets built;
- which vessel comes when;
- how the modes' AI code moves out of the modes;
- what could go wrong.

The shipped system is audited in [`CURRENT_STATE.md`](CURRENT_STATE.md), and the first slice is [`SQUIRREL_SKIM.md`](SQUIRREL_SKIM.md).

---

## 1. Phases

Each phase ships on its own, behind the previous phase's measurements. This is ARCHITECTURE §3's
"no big bang" migration, with dates left to the team.

| Phase | What lands | Exit criterion |
|---|---|---|
| **0 — Unblock** | Commit/drift control resolved by capability (G4). Read-only `VesselTransformer.CommandedRotation`. A non-allocating `Trail.LookAhead`. `CSLogChannel.AI`. **Data-only quick win:** Squirrel throttle 1.0/1.0. **First MPPM check:** does today's local cycler press reach other peers (G5)? | Dolphin byte-for-byte unchanged. Squirrel bots cruise at 60 u/s at every intensity. G5 answered. |
| **1 — Flight controller** | `AIFlightController` (pure) and `FlightModel`, with today's targets routed through them for every vessel and mode. Edit-mode tests. Harness v0. | The orbit-break numbers survive (400/400). Every mode card passes an AI smoke run with no regression. |
| **2 — Driver seam and the skim line** | `AIVesselDriver` and three drivers. `GenericDriver` is today's behaviour, with the cycler as its fallback. `DolphinDriver` is today's commit loop, moved first with parity; G6's `Course` write is then replaced by a controller output and measured against that parity baseline. `SquirrelDriver` is the skim line, the shell solver, the hull veto and the Boost Ring start. Also the Skim Race route hint. | Harness v1 on all four Skim Race tracks beats the §7 baseline of `SQUIRREL_SKIM.md`. Dolphin modes keep parity. |
| **3 — Race craft and objective sources** | Squirrel corners, crystal side-selection, wakes and the full ring policy. `IAIObjectiveSource` plus the legacy adapter, so every mode still works unchanged. | `SQUIRREL_SKIM.md` §7 acceptance at Expert. |
| **4 — Difficulty** | `AIDifficultyProfileSO` tiers and per-bot personality. Decision D1 goes to the selector. The Squirrel section is calibrated in the harness. | A monotone tier ladder. Every tier takes off. A playtest confirms the tiers read as intended. |
| **5 — Fleet rollout** | One driver per vessel in §2 order. Each driver migrates its modes' hooks (§3), deleting mode code. | Per vessel: the driver's harness scenario, plus its modes' AI smoke runs. |
| **6 — Hardening** | Central AI tick (D4). **FrogletTools ▸ AI ▸ AI Pilot Inspector**. The budget checked with 11 bots on a host (a full 12-seat card with one human). An MPPM matrix for every replicated press. | ≤ 0.1 ms per bot per frame and zero GC with 11 bots, profiled. |

## 2. Vessel rollout order

The order optimizes three things:
- **Reuse.** Every slice leaves something the next one needs.
- **Reach.** How many cards a driver unlocks.
- **Risk.** Parity moves of existing code come before new behaviour.

Cards are read from `ArcadeGame*.asset` `Vessels` lists. Speed sources come from the `/arenagame` skill §1 (re-measure before trusting).

| # | Hull | Cards it flies | Speed source | What its driver must learn | Reuses | AI today |
|---|---|---|---|---|---|---|
| 1 | **Squirrel** | **8**: Skim Race, Joust, Scurry, Brood Rush, Astro League, Regatta, Broadside, MP Freestyle | skim energy | skim shell, ribbon following, drift, Boost Ring, wakes, joust lines | — (the foundation) | flies at crystals; cannot take off at intensities 1–2 |
| 2 | **Dolphin** | 7: The Bends, Rampage, Switchback, Brood Rush, Regatta, Broadside, MP Freestyle | drift-charge → discharge; skims for seed energy | crystal commit (moved), cone aim (Echo Sight telegraph), skimming to bank | skim shell (Squirrel) | commit loop inside `AIPilot`; boost on the blind cycler |
| 3 | **Manta** | 7: Redline, Bloomrush, Scurry, Brood Rush, Regatta, Broadside, MP Freestyle | Soar (both triggers; costs yaw) | Soar-vs-yaw curve planning, Sting grazing (bombs planted by skimming rivals and wildlife), Kabloom timing (reach a crystal before the fuses) | flight controller's speed planning | Soar drive lives in `MantaAnalogTurnBoostExecutor` (moves into the driver) |
| 4 | **Rhino** | 7: Cleave, Headlong, Astro League, Brood Rush, Regatta, Broadside, MP Freestyle | ramp on a straight stick, graded | corner optimization on the ramp curve (Headlong's generator already encodes it), the blade line through mass, ball strikes, the energize stance | flight controller | `ram 1`; no trigger use, so an AI can never pop a super-shield |
| 5 | **Sparrow** | **10**: Dog Fight, Salvo, Wildlife Liberation, Breakwater, Co-Op Wildlife Blitz, Scurry, Brood Rush, Regatta, Broadside, MP Freestyle | indefinite boost (free) | gunnery: lead pursuit, fire discipline against the heat phases, base vs heavy rockets by stance, turret stance, reload by gunfire | flight controller, perception | blind cycler (SkyBurst, FullAuto, Boost, ModeSwitchingFire on timers); Dog Fight's stand-off provider |
| 6 | **Scarab** | 7: Scarab Scramble, Tollway, Wrecking Ball, Undertow, Astro League, Regatta, Broadside | throttle ceiling | ball forge and strikes, the juke-dash plate, switch placement onto flora hearts | perception | `TryAutopilotDash` plus hooks in 5 controllers (the dash is known to three) |
| 7 | **Urchin** | 4: Hijack, Skein, Regatta, Broadside | riding its own-colour rails | rail choice and transfers, launch aim, spikes (tap and hold), track projector, reverse flight | `RibbonCursor` (Squirrel) | `ram 1`; Hijack presses spike and slip |
| 8 | **Serpent** | 3: Brood Rush, Regatta, MP Freestyle | fuel pellets (four stacking burns) | pellet timing, scope and sniper aim (a cone), cloak | Sparrow's aiming | blind cycler (ConsumeBoost, CloakSeedWall). These entries predate the scope-and-pellet re-cut; verify them. |
| 9 | **Butterfly** | 1: Waystation | none; the fleet's slowest hull (55 u/s), reaching by the Fold (Time) | fold timing, the exit-gate line, Mass/Dust mode | flight controller | Waystation drives the Fold (moves into the driver) |
| — | Grizzly, Termite, Falcon, Shrike | none shipped | — | `GenericDriver` plus their flight model, for free | — | — |

The Sparrow flies the most cards. **If combat modes are the priority, it can move up to #2.** Its
driver needs a targeting layer that none of the others do, which is why it sits after the racers.

## 3. Mode migration

Today each mode writes a steering lambda and, where it needs one, a hull's ability presses
(CURRENT_STATE §4). After migration:
- a mode only says **what** it wants, through an `IAIObjectiveSource`;
- the vessel's driver decides **how**.

Each migration deletes mode code. Each row moves when its vessel's driver lands.

| Mode(s) | Hook today | Becomes | Moves with |
|---|---|---|---|
| Skim Race | platform default (crystals) | default source + a route hint (the ribbon's `Trail`) | Squirrel (Phase 2) |
| Joust, Scurry, Rampage, Salvo, Brood Rush | platform default (crystals or `seekPlayers`) | default source; nothing to delete | their drivers |
| `GateRaceController`: Switchback, Headlong, Breakwater, Skein, Redline, Regatta, Waystation | a two-waypoint gate approach with a latched side, plus a re-implemented crystal detour (G8); `TryOverrideAim` for attached vessels | `ThreadGate` objectives (axis + capture radius) composed with crystals, so **the detour is deleted**; attached aim moves to the Urchin driver | the gate-race platform, first used by Dolphin (Switchback) |
| The Bends | `SetDriftLookTargetProvider` (aim the cone at a rival) | an `AimAt` objective; the Dolphin driver aims | Dolphin |
| Dog Fight | `seekPlayers` + stand-off provider with a latched break-off | `Engage` objectives; stand-off and break-off become Sparrow behaviours | Sparrow |
| Cleave, Wildlife Liberation | steering provider | `Strike` / `Engage` objectives (mass to cut, creatures to hunt) | Rhino, Sparrow |
| Astro League, Scarab Scramble | steering provider (striker target, roll-home) | `Strike` objectives on the ball | Rhino, Scarab |
| Wrecking Ball, Undertow | provider + `TryAutopilotDash` | `Strike` / `Engage` objectives; the dash becomes a Scarab ability policy | Scarab |
| Tollway | provider + replicated switch presses | anchor objectives (flora hearts); switch placement becomes a Scarab ability policy | Scarab |
| Hijack | provider + replicated spike and slip presses | loot objectives + rail routes; spikes and slip become Urchin ability policies | Urchin |
| Broadside | `seekPlayers` + `TryAutopilotDash` + replicated spike tap | `Engage` objectives; every hull's weapon comes from its own driver | all eight hulls |
| Waystation | `OnServerTick` Fold drive | the next cluster as the objective; the Fold becomes a Butterfly ability policy | Butterfly |

**Temporary compatibility.** During migration, the legacy adapter keeps
`SetExternalTargetProvider` and `SetDriftLookTargetProvider` working (ARCHITECTURE §2.2). They are
deleted when the last caller moves.

## 4. Risks and open decisions

| Risk | Why it matters | Mitigation |
|---|---|---|
| Parity regressions | Phase 1 routes **every** mode's bots through a new controller | Harness v0, plus an AI smoke run per mode card. During migration, a driver is opt-in per vessel; the opt-in is deleted when the vessel's rollout completes. |
| Harness transcription drift | The harness runs the shipped AI code against a *copy* of the flight model | Read every constant from the assets by key (`regatta_balance.py`, `squirrel_skim_model.py`), and pin the transcription with tests the way the existing harnesses do |
| Replication | AI runs server-only. A press with visible output that is not replicated exists on one machine only. | R5, plus Phase 0's MPPM check of G5, plus an MPPM row per replicated press (Phase 6) |
| Host CPU | The host is a player's machine, running up to 11 bots | 0.1 ms per bot, staggered queries, a central tick (D4), and profiler markers from day one |
| Difficulty that does not read | Numbers in a harness are not feel | The harness calibrates; playtests confirm; every knob is in an SO |
| Emergence and fairness | A bot must not win on information a human lacks | Route hints only expose what a human sees (the track). Hidden state (element levels, cooldowns) stays hidden (D3). Difficulty is imperfection, never stats (R7). |
| Content surprises | Tuning targets depend on the tracks (G13) | Decide intensity 2's anchors and intensity 4's hairpin before calibrating Phase 4 |
| Doc drift | AI facts are duplicated in vessel docs | A vessel's driver is registered in the `/vessel` contract (§5 below), and the AI docs point there rather than copy it |

**Open decisions.** D1–D4 are in ARCHITECTURE §4:
- difficulty source;
- adaptive difficulty;
- perception fairness;
- the central tick.

D4 is recommended as soon as Phase 2 lands. From then on a bot runs several layers that should
share one staggering scheme and one profiler marker, instead of N `Update`s.

The slice's own questions are in SQUIRREL_SKIM §9.

## 5. How a future vessel joins

A new hull gets flight for free: the controller reads its `FlightModel` from the live transformer.
What it adds is a driver.

1. **Behaviours:** how it gets speed, how it turns, and what it must avoid.
2. **Ability policies**, resolved by CAPABILITY (`TryGetBoundAction<T>`), never by control or device.
   A press whose output other peers must see is replicated.
3. **Speed-source parity.** If the hull's speed is an ability (like the Manta's Soar), the driver
   must use it, or an all-AI domain in that hull cannot win. This is the `/arenagame` rule.
4. **Difficulty section** in `AIDifficultyProfileSO`.
5. **Harness scenario** for its signature mode, with acceptance numbers.
6. **Register** the driver in the `/vessel` skill's AI-parity contract, so the next vessel's
   checklist asks for one.

**Until a driver exists**, `GenericDriver` flies the hull at the objectives with today's behaviour.
That is always a working bot, never a crash.
