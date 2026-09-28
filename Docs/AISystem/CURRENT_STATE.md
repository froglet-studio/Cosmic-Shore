# The AI pilot system as shipped — audit (2026-09-28)

**What exists, how one frame of a bot actually runs, where modes and vessels bend it, what works
and must survive any rework, and what is broken or missing** — measured off the shipped code and
assets, not recalled. Read this before changing `AIPilot`; read [`ARCHITECTURE.md`](ARCHITECTURE.md)
for where it is going and [`SQUIRREL_SKIM.md`](SQUIRREL_SKIM.md) for the first vessel it is going
there on.

The one earlier design doc, `Assets/_Scripts/Controller/AI/AI_ORBIT_BREAK.md`, is still correct and
still load-bearing: it covers the Dubins orbit break and the objective-selection fix, and nothing
here supersedes it.

---

## 1. The pieces

| File | Role |
|---|---|
| `Controller/AI/AIPilot.cs` (~1,100 lines) | **The whole pilot**, one `MonoBehaviour` on every vessel prefab (it is in `VesselStatus`'s `[RequireComponent]` set): target selection, the steering law, throttle, the Dolphin's commit-drift loop and aim telegraph, a blind ability cycler, the orbit break, and the mode hooks. |
| `Controller/AI/PursuitReachability.cs` | Pure math, edit-mode tested (`Tests/Editor/PursuitReachabilityTests.cs`): the Dubins turning-circle test, `OrbitDetector`, `AIObjectiveScoring` (nearest-objective pick with commitment hysteresis). **The model for how new AI math should look.** |
| `Controller/AI/AI_ORBIT_BREAK.md` | The only AI design doc before this folder. |
| `Controller/AI/AIGunner.cs` | A vestige — an empty class whose body is commented out. Nothing reads it. |
| `Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` | Spawns AI players + vessels (`SpawnAIs`) and configures each pilot (`ConfigureAIPilotForMode`: `seekPlayers` for Joust / Dog Fight / Broadside, `skill = intensity × 0.25`). |
| `ScriptableObjects/SO_AIProfileList.cs` | `AIProfile` = **a name and an avatar**. No personality, no difficulty. |
| `R_VesselActionHandler.TryGetInputForAction<T>` / `TryGetBoundAction<T>` | **Capability lookup** — "which control does THIS hull put an ability of type T on". The right seam; already exists. |
| `R_VesselActionHandler.PerformShipControllerActionsReplicated` | The replicated press path an AI must use for anything other peers must see (an AI runs server-only). Already exists. |
| `IAimTelegraphAction` | A capability marker interface — the first "ask for the concept, not the vessel" use. |
| `MantaAnalogTurnBoostExecutor` (autopilot Soar drive), `ScarabJukeController.TryAutopilotDash` | **Per-vessel AI knowledge living in vessel code** — the right direction, but ad hoc. |

## 2. One frame of an AI pilot

`AIPilot.Update`, in order (grep the symbols; line numbers move):

1. **Stationary** → release the aim telegraph, end any break-off, return.
2. **Target.** `seekPlayers` → the chosen opponent's LIVE position (re-selected by a coroutine at
   0.5 s / 0.1 s). Otherwise the crystal `UpdateCellContent` picked (event-driven on the cell's
   `OnCellItemsUpdated`, through `AIObjectiveScoring.Select`). A mode's
   `SetExternalTargetProvider` overrides both.
3. **Orbit break** (`UpdateOrbitBreak`): the predictive turning-circle test plus the empirical
   `OrbitDetector`; while extending, steer at an escape point instead of the objective.
4. **Commit drift** (only where the prefab authors `drift: 1` — the Dolphin): once the objective is
   within 25.8° of `Course`, **write `VesselStatus.Course = desiredDirection`**, press
   `LeftStickAction`, light the aim telegraph, and swing the nose to the mode's aim point or the
   densest hostile mass (`Cell.GetExplosionTarget`).
5. **Steering law** → writes `XSum`/`YSum`/`YDiff` (two-stick hulls) or
   `EasedLeftJoystickPosition` (one-thumb hulls) on the vessel's `InputStatus`. See §7 G1 for what
   the law actually does.
6. **Throttle** → `XDiff = (aligned && ram) ? 1 : clamp(throttle)`, where
   `throttle = lerp(defaultThrottleLow, defaultThrottleHigh, skill)` and creeps up by
   `throttleIncrease` every second, forever.
7. **Abilities** run on their own: one coroutine per `abilities` entry does
   `StartAction → wait Duration → StopAction → wait Cooldown`, on the SO directly, forever.

**Where it runs.** Server only: `Player.StartPlayer` returns before the autopilot branch on
`IsNetworkClient`. `InputStatus`'s stick fields are owner-write `NetworkVariable`s and the server
owns AI players, so a bot's stick values replicate every frame; its motion replicates through the
vessel's own sync. `InputStatus.ActiveInputDevice` is a plain LOCAL field that nothing sets for an
AI, so every AI reports the enum default, **`Touch`** — which decides which device-override binding
map its presses resolve against (§7 G4) and makes every AI drift binary (§7 G9).

## 3. What "skill" does today

- `skill = clamp01(SelectedIntensity × 0.25)` — derived from the **arena** intensity. There is no
  difficulty setting anywhere; intensity is doing both jobs.
- It lerps five High/Low pairs. **Only the throttle reaches behaviour:**

| field | what happens to it |
|---|---|
| `defaultThrottleLow/High` | live — becomes `XDiff` |
| `defaultAggressivenessLow/High` | read once at `Initialize`, then **overwritten with `100f` every frame** in `Update` |
| `aggressivenessIncreaseLow/High` | read only by a commented-out line |
| `avoidanceLow/High` | declared, **never read** |
| `throttleIncreaseLow/High` | live, but it creeps `throttle` up forever (the write clamps it) |

- Three prefabs author identical High/Low throttles (Butterfly 0.6/0.6, Grizzly 0.6/0.6, Urchin
  0.8/0.8), so on them skill changes nothing at all.
- Menu / lava-lamp pilots are never configured and keep the prefab's `skillLevel` (10 on the
  Squirrel, which clamps to 1).
- `raycastWidth/Height` and the `CornerBehaviors` table are built in `Initialize` and never read —
  obstacle avoidance was scaffolded and never wired. `ShootLaser`, `CalculateRollAdjustment` and
  `SigmoidResponse` sit in an "Unused Methods" region.

## 4. How modes bend the AI

Eleven controllers install hooks; `GateRaceController` alone serves seven gate-race modes.

| Controller (modes) | Hook | Vessel knowledge it carries |
|---|---|---|
| `GateRaceController` (Switchback, Headlong, Breakwater, Skein, Redline, Regatta, Waystation) | `SetExternalTargetProvider`: two-waypoint gate approach with a latched side, plus a crystal **detour it has to re-implement** because the provider replaces crystal seeking; `TryOverrideAim` for vessels attached to geometry | — |
| `WaystationController` | `OnServerTick` drives the Butterfly **Fold** through `PerformShipControllerActionsReplicated`, reading the ability's own SO via `TryGetBoundAction` | Butterfly Fold |
| `TollwayController` | provider + replicated switch presses | Scarab switch |
| `HijackController` | provider + replicated spike and slip presses | Urchin spike, slip |
| `BroadsideController` | `seekPlayers` + `TryAutopilotDash` + a replicated spike tap | Scarab dash, Urchin spike |
| `WreckingBallController`, `UndertowController` | provider + `TryAutopilotDash` | Scarab dash |
| `ScarabScrambleController` | provider (roll your ball home) | — |
| `AstroLeagueController` | provider (billiard-style striker target) | — |
| `DogFightController` | `seekPlayers` + provider (stand-off, latched break-off) | — |
| `BendsController` | `SetDriftLookTargetProvider` (aim the cone at a rival) | — |
| `CleaveController`, `WildlifeLiberationController` | provider | — |
| Skim Race, Scurry, Joust, Rampage, Salvo, … | platform default (crystals, or `seekPlayers`) | — |

**The pattern:** each mode writes a small steering lambda, and whichever mode first needed a hull
to use an ability learned that hull's controls — so the Scarab's dash is known to three modes, the
Urchin's spike to two, and a new vessel in an old mode, or an old vessel in a new mode, needs a mode
edit. That is the coupling [`ARCHITECTURE.md`](ARCHITECTURE.md) §2.4 removes.

## 5. How vessels bend the AI

`AIPilot` as authored on each vessel prefab (`Assets/_Prefabs/Spacevessels/*.prefab`):

| Vessel | throttle Low..High | ram | drift | `abilities` (the blind cycler) |
|---|---|---|---|---|
| Butterfly | 0.6..0.6 | 0 | 0 | — |
| Dolphin | 0.4..0.8 | 0 | **1** | DolphinDrift, DriftTrail, ChargeBoost (2 s on / 2 s off) |
| Falcon, Manta, Scarab, Shrike, Termite | 0.2..0.9 | 0 | 0 | — |
| Grizzly | 0.6..0.6 | 0 | 0 | — |
| Rhino | 0.3..0.6 | 1 | 0 | GrowTrail (0/0) |
| Serpent | 0.2..0.9 | 0 | 0 | ConsumeBoost (every 8.1 s), CloakSeedWall (every 4.3 s) |
| Sparrow | 0.2..0.9 | 0 | 0 | SkyBurst (2 s on / 5 s off), FullAuto (3/0.8), Boost (2/10), ModeSwitchingFire (2/10) |
| **Squirrel** | 0.2..0.9 | 0 | **0** | **—** |
| Urchin | 0.8..0.8 | 1 | 0 | — |

Autopilot drives that live outside `AIPilot`: the Manta's Soar (`MantaAnalogTurnBoostExecutor`),
the Scarab's dash (`ScarabJukeController.TryAutopilotDash`). The fleet-facts table in the
`/arenagame` skill (§1) records which hulls an autopilot can reach its own speed source on: the
Manta yes, the Rhino by the gesture, the Urchin by riding, the Scarab by throttle only — **the
Dolphin, Squirrel, Serpent and Sparrow no.**

## 6. What works — keep it through any rework

- **The orbit break** — exact Dubins test with a capture radius, the empirical detector, the
  closing-cosine guarantee, runway in SECONDS. Measured 373/400 → 400/400 (`AI_ORBIT_BREAK.md`).
- **`AIObjectiveScoring`** — like-for-like distances plus commitment hysteresis; the fix for the
  "swerves away at the last second" report.
- **Capability lookup and the replicated press path** (`TryGetBoundAction<T>`,
  `PerformShipControllerActionsReplicated`, `IAimTelegraphAction`).
- **`TakeModeHooksFrom`** (the arena pilot swap: hooks follow the bot, not the hull), idempotent
  `StartAIPilot`, and a `StopAIPilot` that leaves the hull holding nothing.
- **`RetargetCell`** for the mode preview's satellite arena.
- **The telegraph honesty rule** — announce an aim only while committed to it.
- **The pure-static, tested style** of `PursuitReachability`.

## 7. Defects and gaps

Numbered so later docs can cite them. Each is measured; where a claim is not yet verified it says so.

**G1 — The steering law cannot hold a line.** It computes
`angle = deg(asin(clamp(2·sin²θ)))` for a heading error θ (at range > 7 u) and multiplies by the
cross-product component, so near zero it is roughly **cubic** in θ and it saturates at ~12°:

| heading error | stick | yaw rate (Squirrel, 120°/s) | sideways drift at 300 u/s |
|---|---|---|---|
| 1° | 0.0006 | 0.1°/s | 5 u/s |
| 3° | 0.016 | 2.0°/s | 16 u/s |
| 5° | 0.076 | 9.1°/s | 26 u/s |
| 10° | 0.60 | 72°/s | 52 u/s |
| ≥ 12° | 1.00 | 120°/s | ≥ 62 u/s |

Below ~3° it barely corrects at all; above 12° it is bang-bang. It has no damping term, and it
ignores the steering plant's own lag: `VesselTransformer.RotateShip` integrates the stick into
`accumulatedRotation` and the transform then **slerps toward it at `LERP_AMOUNT` 1.5/s — a
first-order lag of τ ≈ 0.67 s**, 200 u of travel at 300 u/s. Fine for "fly at a crystal"; unable to
hold anything measured in single units, which is what skimming asks for.

**G2 — Skill is inert beyond throttle, and it is really intensity** (§3).

**G3 — The Squirrel AI cannot accelerate at intensities 1–2, and has no kit.** Its speed is skim
energy and skim energy has a take-off threshold (`SQUIRREL_SKIM.md` §3): at the AI's intensity-1
throttle (XDiff 0.375) it needs **160%** skim duty to take off, i.e. never; at intensity 2, 109%,
never. `drift: 0`, `abilities: []`.

**G4 — The commit loop presses a hardcoded control that a device-override hull does not answer.**
`AIPilot.CommitControl = LeftStickAction (2)`. The Squirrel's **shared** binding map is empty; it
binds its drift only under device overrides (touch `OnlyLeftStickAction (12)`, gamepad `2`), and an
AI's `ActiveInputDevice` is the default `Touch` (§2), so a press of `2` resolves to nothing —
authoring `drift: 1` on the Squirrel's AIPilot would silently do nothing. Abilities must be found by
capability (`TryGetBoundAction<DriftActionSO>` → `12` on this hull), never by a fixed control.

**G5 — The ability cycler presses locally, on blind timers.** `UseAbilityCoroutine` calls
`StartAction` on the SO directly, bypassing `R_VesselActionHandler` and its replication, and fires
whether or not there is anything to fire at (the Sparrow's skyburst every ~7 s, full-auto 3 s on /
0.8 s off). CLAUDE.md already records the consequence for placed mass (*an AI's placed structure
must go through the replicated path*); **whether AI gunfire or the Serpent's cloak wall reach
other peers is unverified** — check in MPPM before relying on either.

**G6 — The commit drift writes state no human can.** `VesselStatus.Course = desiredDirection` is an
instant course snap of up to 25.8°. Small, and it is what makes the Dolphin's AI drift work today
(`SQUIRREL_DRIFT.md` §4 had to preserve it), but it is the one place the AI does not play by the
input surface.

**G7 — Vessel ability knowledge is duplicated across mode controllers** (§4).

**G8 — A mode's steering provider replaces crystal seeking wholesale** (the trap The Bends records),
so any mode that wants crystals AND its own objective re-implements the detour
(`GateRaceController.FindDetourCrystal`).

**G9 — Every AI drift is binary.** `VesselTransformer.GetTriggerSum` reads the analog trigger only
for `Gamepad`; an AI reports `Touch`, so its drift is always full-strength (eased over ~83 ms).

**G10 — The AI is blind to prisms.** It never predicts a hull contact, never looks for skim
contacts, and the only prism query it makes is the drift-aim mass cluster. On the Squirrel a single
hull contact resets the whole speed economy (`VesselResetBoostPrismEffect`). Flying at crystals, its
straight line passes through the Skim Race ribbon on 6–22% of legs (`squirrel_skim_model.py` §10). It is also blind to
the richest skim source in the mode: every Squirrel's wake is two rails a prism every 7 u, against the
ribbon's 12 u, laid around the very line a follower flies (`SQUIRREL_SKIM.md` §5.8).

**G11 — `AIGunner` is dead**, and `AIProfile` carries no personality (§1).

**G12 — Doc drift found on the way:** `SKIMRACE.md` said 1–4 players and "Squirrel, Manta,
Sparrow"; `ArcadeGameSkimRace.asset` is **Squirrel-only, 1–12 players**. Corrected in the same
branch as this audit.

**G13 — Skim Race content facts** (not AI defects — flagged for design, measured by
`Tools/Build/squirrel_skim_model.py` §8, §9):
- **Intensity 2's crystal anchors sit 28–35 u off its own spline** (the other three intensities'
  anchors are on the ribbon to within 2 u), so its crystals land up to ~70 u off the ribbon against
  ~36 u everywhere else. With the crystal's 24 u trigger, that is the difference between a crystal
  you take from the skim zone (≤ ~5 u beyond it, intensities 1, 3, 4) and one you leave the ribbon
  for (p95 30 u beyond it, intensity 2).
- **Intensity 4's loop closes on a 156.7° hairpin** at waypoint 0 (the start/finish) — the only
  corner over 71° on any of the four tracks. Deliberate or a closure artifact? Worth a look either
  way: it is unskimmable at speed for humans and AI alike.
