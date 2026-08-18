# The Speed Tunnel — PLATFORM LAW

**Speed is a property of every vessel, so the visual language for speed belongs to every
vessel.** As the local pilot's vessel gets faster, the gameplay camera's field of view narrows
below its home value while the URP Panini projection distance falls below the profile's own
baseline with it. The tightening FOV magnifies the frame centre (telephoto push-in) as the
Panini compression relaxes toward rectilinear — tunnel vision, with **no camera-distance
change at all**. A quasi dolly zoom, sold entirely through optics.

This is **not a feature a vessel or a game mode may choose**, and it must not be possible to
author one in which it is off — the same standing as the camera↔vessel prism occlusion
corridor (`Docs/PRISM_ANIMATION.md` §4.7), whose enforcement shape this copies exactly.

## §1 The law

```
effect01 = clamp01( inverseLerp(minEffectSpeed, maxEffectSpeed, VesselStatus.Speed) )
fov      = max(1°, homeFov − fovDrop × effect01)
panini   = profileBaseline − paniniDrop × effect01
```

Three properties are load-bearing. Do not relitigate them.

- **The mapping is ABSOLUTE.** One global function of speed, shared by the entire fleet, so
  **the same speed on any vessel produces the same visual**. A vessel that cruises faster sits
  deeper in the tunnel than one that crawls — because it *is* going faster. There is no
  per-vessel window, no per-vessel scalar, and no normalization that makes every vessel reach
  full effect at its own top speed. That alternative was considered and rejected; it would
  destroy the one property the law exists to guarantee. It is also *why* the law is
  un-authorable: there is no per-vessel number anywhere, so there is nothing a vessel could
  author to escape it.
- **The drive signal is measured `VesselStatus.Speed`, never boost state.** The effect follows
  every speed source that exists or will exist — trigger boosts, constant-acceleration ramps,
  skim charges, throttle modifiers, crystal buffs — with nothing to bind and nothing to keep
  in step. An ability that makes you fast gets the visual for free and cannot forget to.
- **Home values are whatever the game is actually running with.** FOV home is captured from the
  live camera the frame the effect takes over, and Panini home is the profile's own state; the
  effect only ever moves DOWN from there and returns exactly. Never anchor either to a constant
  or a settings default — that reads as a snap onto foreign values the instant speed rises.

## §2 What makes it strict

Four structural properties, mirroring the occlusion corridor's four layers:

| # | Layer | Mechanism |
|---|---|---|
| 1 | **Binding** | `VesselController.Initialize` under `IPlayer.IsLocalPilot` — the one method every vessel must call to become a player's vessel, on every spawn path (single-player, multiplayer, menu autopilot, runtime swap). Nothing per-vessel, nothing per-scene, therefore nothing to forget. `IsLocalPilot` (not `IsLocalUser`) so the legacy non-networked single-player path is covered — that gap is exactly the escape hatch the law must not have. `ChangePlayer` re-evaluates too (for BOTH platform laws), so the Cellular Duel ownership swap can't strand either of them on the hull the AI inherited. |
| 2 | **One driver** | `VesselSpeedTunnel` is a **static class** with a single hidden `DontDestroyOnLoad` `LateUpdate` publisher installed by `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`. Not tidiness: `PostProcessingManager.SetSpeedTunnelPanini` is one global override with **no ref-counting**, so N per-vessel writers race it and an outgoing vessel's teardown stomps the incoming vessel's value mid-swap. One driver cannot race itself. |
| 3 | **Fail loud** | Missing `PostProcessingManager` (Panini half inert) and an unexpected camera-controller type (FOV half inert) each warn **once per session**, naming the fix. A null controller — Cinemachine driving the menu — is a designed state and is deliberately silent. |
| 4 | **Gates** | `SpeedTunnelLawTests` (edit-mode, asset-only) + **FrogletTools > Vessels > Validate Speed Tunnel Law**. Both assert that no vessel prefab carries its own driver *or a missing-script residue of the retired one*, that **every** bind site sits under an `IsLocalPilot` guard, that the drive site passes **raw** speed, and that the config is sane. Every one of those predicates lives once — `SpeedTunnelConfigSO.IsSane` and `SpeedTunnelLawSource` — and all three gates call it, so they cannot drift. Two of these checks are written the way they are because the obvious version is *vacuous*: prefab YAML records a script by GUID and never by class name, and a whole-file `Contains("IsLocalPilot")` stopped being a gate the moment `ChangePlayer` grew a second occurrence. |

The sanctioned hold is `VesselSpeedTunnel.SetSuppressed`, and it has exactly one shape: **a
vantage posed independently of the pilot's vessel**. It is a **hold, not an opt-out** — the vessel
binding survives it, so nothing has to remember to re-point the tunnel afterwards. Two callers
qualify, and a third would need to meet the same bar:

- `CameraManager.BeginManualReplayCamera` / `RestoreGameplayCamera`. A replay camera is posed by
  hand and `AstroLeagueGoalReplay` reads its field of view to fit the shot, so a live FOV write
  would both fight the pose and silently mis-frame the replay.
- `MainMenuCameraController`, while a **non-vessel-framing** menu config owns the view — today
  only `MenuCameraRigKind.LavaLamp`. This is the one case the "menu is a designed state" note
  below does *not* cover. In the menu the FOV half is already inert (the menu owns the scene
  camera, so `GetActiveController()` is null), **but the Panini half is global and does not care
  which camera renders**, so the autopilot vessel's fluctuating speed keeps warping the frame.
  While a vessel-framing config is active that is fine — the camera is riding the ship and the
  warp tracks the motion being watched. The lava lamp is a detached orbital shot of the *cell*
  that never follows the vessel, so there is no speed being sold to anyone and the pumping Panini
  distance reads as an unexplained rhythmic lens warp. The hold is re-derived from live state at
  the top of `LateUpdate` **before** that method's early returns (so it cannot latch on when
  freestyle begins) and lifted in `OnDisable` (which covers both teardown and a merely-disabled
  controller). It only ever writes the flag on its own edge, so it can never release the replay
  camera's hold — the flag has no ref-counting, exactly like the Panini override it guards.

Two details of that hold are load-bearing. It is **immediate**: `Tick` tests suppression at the
point of application, not only when computing the target, because this driver carries smoothing
state and the same call that suppresses also *cuts to* the replay camera — a target-only hold
would spend the decay writing FOV onto exactly the camera it is protecting. And the lift in
`RestoreGameplayCamera` is **unconditional and first**, above that method's own follow-target
early return: a replay can finish a frame after its scene tore down, and returning before the
lift would latch both platform laws off for the rest of the session (these statics are otherwise
reset only by their `RuntimeInitializeOnLoadMethod` installers, once per app launch).

## §3 Tuning

`Assets/Resources/SpeedTunnelConfig.asset` (`SpeedTunnelConfigSO`) is the **only** tuning
surface for the entire fleet. With no asset the SO's own defaults apply, so the law holds with
zero authoring.

| Knob | Shipped | Meaning |
|---|---|---|
| `minEffectSpeed` | 70 | Below this the effect is exactly zero and costs nothing. |
| `maxEffectSpeed` | 280 | Full strength; faster saturates rather than distorting further. |
| `fovDrop` | 25° | Degrees removed from home FOV at full effect. |
| `paniniDrop` | 0.5 | Fall below the profile's Panini baseline (gameplay profile ships 0.7). |
| `responsiveness` | 12/s | Rounds off discontinuities (spawns, teleports); speed is already continuous. |
| `enabled` | on | Global debug switch for A/B-ing the law. **Not** an authoring surface. |

### Where the fleet lands in the shared window

Cruise = full throttle, unboosted (`DefaultThrottleScaler + DefaultMinimumSpeed`). Top applies
the vessel's **real** boost source to the *scaler only* and then adds the minimum
(`DefaultThrottleScaler × boost + DefaultMinimumSpeed` — the minimum is added AFTER the multiply,
per `VesselTransformer.ComputeThrottleTarget`; that is why the Rhino's ×6 gives 310 and not 360).

**FrogletTools > Vessels > Validate Speed Tunnel Law** prints the **cruise** column live and is
the gate for it. Its `top(rest)` column is NOT this table's top: the tool reads only the prefab's
resting `VesselStatus.boostMultiplier`, while three vessels get their real top from elsewhere —
the Rhino's ×6 from `RhinoRampBoostAction.asset`, the Squirrel's ×5 from the skim-boost clamp, the
Serpent's from its consume-boost — none of which a prefab sweep can resolve. Teaching the tool to
chase them is a much larger change for a column that is explicitly a report, not a check, so the
boosted numbers below are maintained by hand.

| Vessel | cruise | effect | top speed | effect | notes |
|---|---|---|---|---|---|
| **Manta** | 180 | **0.52** | 720 | 1.00 | `DefaultThrottleScaler` 180 — 3.6× the fleet norm |
| Rhino | 60 | 0.00 | 310 | 1.00 | ramp boost SO sets ×6 |
| Squirrel | 60 | 0.00 | 300 | 1.00 | skim boost clamps to ×5 |
| Serpent | 60 | 0.00 | 210 → 330 | 0.67 → 1.00 | Time elemental up to ×1.6 |
| Dolphin | 60 | 0.00 | 210 | 0.67 | |
| Falcon / Shrike | 60 | 0.00 | 210 | 0.67 | |
| Urchin | 50 | 0.00 | 200 | 0.62 | `Speed` is 0 while trail-attached |
| Grizzly | 50 | 0.00 | 200 | 0.62 | |
| Sparrow | 35 | 0.00 | 110 | 0.19 | |
| Termite | — | — | — | — | `CommandVesselTransformer` never writes `Speed`, so the Termite structurally cannot tunnel. The validator prints "—" for it rather than its inert throttle fields. |

**Read the Manta row before tuning.** It is not a bug and the fix is not a Manta-specific
number — under an absolute law a vessel that cruises at 180 u/s *should* look faster than one
cruising at 60, and the Manta genuinely does cruise three times faster than the rest of the
fleet. It does mean the Manta flies with roughly half the tunnel applied at all times. If that
reads as too much, the correct lever is **the shared floor** (`minEffectSpeed`), moved with the
whole fleet in view — not a per-vessel exception, which the law forbids, and not a Manta
throttle change, which is a flight-feel decision that belongs to whoever tuned it to 180.

## §4 Files

| Role | File |
|---|---|
| The law + driver | `Assets/_Scripts/Utility/VesselSpeedTunnel.cs` |
| Tuning + the pure math | `Assets/_Scripts/ScriptableObjects/SpeedTunnelConfigSO.cs` |
| Shipped tuning asset | `Assets/Resources/SpeedTunnelConfig.asset` |
| Binding site (the whole per-vessel wiring) | `Assets/_Scripts/Controller/Vessel/VesselController.cs` — `Initialize`, `ChangePlayer`, `OnDestroy` |
| Panini override (single writer) | `Assets/_Scripts/Controller/Managers/PostProcessingManager.cs` — `SetSpeedTunnelPanini` |
| Sanctioned suppression | `Assets/_Scripts/Controller/Managers/CameraManager.cs` — `BeginManualReplayCamera` / `RestoreGameplayCamera` |
| Shared gate predicates | `Assets/_Scripts/Utility/SpeedTunnelLawSource.cs` (editor-only; the two gates live in assemblies that cannot see each other, so the rule is written once here) |
| Asset gate (menu) | `Assets/_Scripts/Editor/SpeedTunnelLawValidator.cs` |
| Asset gate (test) | `Assets/_Scripts/Tests/EditMode/SpeedTunnelLawTests.cs` |

## §5 In-editor verification

1. **Every vessel, not just two.** Fly Rhino, Manta, Dolphin, Squirrel, Sparrow and Serpent in
   any game mode. Each should tunnel purely as a function of how fast it is going — the Rhino
   only under its ramp boost, the Manta noticeably at plain cruise, the Sparrow barely at all.
   Confirm nothing was wired per vessel: the effect is present on vessels that never had the
   component.
2. **Same speed, same look.** Boost a Dolphin (top ~210) and a Serpent (top ~210) side by side —
   identical tunnel. That equality IS the law.
3. **Return home exactly.** Drop back to cruise; FOV and Panini must land on their pre-boost
   values. Change the FOV slider in Settings *while* boosting — the new setting must survive the
   release rather than snapping back to the old home.
4. **Vessel swap** (menu freestyle vessel changer, or Cellular Duel round boundary): the tunnel
   follows the new hull with no stuck Panini and no flicker at the handover.
5. **Menu freestyle**: Cinemachine drives the view, so only the Panini half applies — expected,
   and no warning should be logged for it.
6. **MPPM, two clients**: a remote or AI vessel boosting must not move YOUR camera or post stack.
7. **Astro League goal replay**: during the replay the tunnel is held closed and the replay
   framing is unaffected; it resumes on return to play.
8. **Gates**: run **FrogletTools > Vessels > Validate Speed Tunnel Law** (expect PASS + the fleet
   table) and the `SpeedTunnelLawTests` fixture in the Test Runner (expect all green).

## §5.1 Verification matrix

What was actually verified, and how. "Compiles by inspection" means exactly that — **no C#
compiler and no Unity ran on this branch**; every runtime claim below is the human's gate.

| System | Verified how |
|---|---|
| `SpeedTunnelConfigSO` math (`Effect01`, `FovFor`, `PaniniOffsetFor`, `IsSane`, inverted-window clamp) | Edit-mode tests (`SpeedTunnelLawTests`), values hand-checked |
| `SpeedTunnelLawSource` predicates (bind-gating, raw-drive-site, retired-GUID probe) | Executed offline against the real source + prefab files; the GUID probe additionally proven to fire on `HEAD~1`'s prefabs and not on HEAD's |
| Prefab surgery (component removed from Manta + Rhino) | Structural: no dangling component refs, no duplicate fileIDs, script-guid delta vs merge-base is exactly the retired driver on Rhino and empty on Manta |
| `SpeedTunnelConfig.asset` | Key-by-key diff against the class's `[SerializeField]` names, both directions — no silent drops, no silent defaults |
| `VesselSpeedTunnel` runtime behaviour (bind, decay, camera swap, home capture/restore, warn-once) | **Compiles by inspection only — not run.** In-editor §5 below is the gate |
| Suppression hold (immediacy + unconditional lift) | **Compiles by inspection only — not run.** §5 step 7 is the gate |
| `ChangePlayer` re-binding (both laws) | **Compiles by inspection only — not run.** §5 step 4 is the gate |
| Fleet table numbers | Recomputed from each prefab's serialized `DefaultThrottleScaler` / `DefaultMinimumSpeed` / `boostMultiplier`; boosted tops read from the action SOs by hand |

## §6 Follow-ups

- The **pre-game cinematic** poses the camera while `CustomCameraController` is disabled but
  still the active controller, so the tunnel can write FOV during it. Harmless today (the vessel
  is not moving fast), but it is the second candidate for `SetSuppressed` if it ever isn't.
- `Camera.orthographic` is sticky and one-way (`VesselCameraCustomizer` only ever sets it true).
  An orthographic vessel would permanently disable the FOV half on the shared camera, silently.
- A `PostProcessingManager` whose `Volume` is missing makes the Panini half a silent no-op —
  `SetSpeedTunnelPanini` returns early and the driver's warn-once only fires when the manager
  itself is absent. Broken-prefab territory rather than a normal state, but it is the one
  remaining way the law can be half-inert without saying so.
- **Residual home-FOV seam.** `CameraManager.ApplyCameraGraphicsSettings` writes the settings
  FOV straight onto every managed camera, and it runs from `SetupGamePlayCameras` /
  `SetupEndCameraFollow` as well as from the settings events. The settings-event case is
  handled (the driver re-captures home on `DisplayGraphicsSettings.OnFieldOfViewChanged`), and
  the setup cases normally coincide with a camera change, which re-captures anyway — but a
  setup call that keeps the SAME camera while the tunnel is engaged would leave home stale for
  the rest of that engagement. Not reachable today (those calls happen at spawn, before speed
  builds); the fix if it ever is would be to read home from
  `DisplayGraphicsSettings.Current.FieldOfView` when available.
