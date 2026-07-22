# Rhino Ramp Boost + Speed-Tunnel Quasi Dolly Zoom

The Rhino's full-speed-straight reward. Holding full throttle with no rotation input
raises `InputEvents.FullSpeedStraightAction` (from the input strategies' deviation
threshold), which the Rhino maps to `RhinoRampBoostAction` + `GrowTrailAction`. The boost
is a **constant-acceleration ramp**, and it is sold optically by an **inverse quasi dolly
zoom** — field of view and Panini projection start at the same home values every vessel
runs with and drop below them proportionally to live speed.

## Speed model

- **Engage** (`RampBoostActionSO` → `RampBoostActionExecutor.Begin`): `IsBoosting` on,
  `BoostMultiplier` = `maxBoostMultiplier` (top-speed target), and the transformer enters
  **constant-rate speed tracking** — `VesselTransformer.SetSpeedTrackingRate(accelerationPerSecond)`
  makes `AdvanceSpeed` use `MoveTowards` (linear, steady slope) instead of the default
  exponential lerp. The TIME elemental multiplies the acceleration (Element → parameter),
  and separately multiplies the top speed inside the transformer as for any boost.
- **Release** (input deviation, turn end, or component disable): boost state restores and
  tracking switches to `returnPerSecond` — a fast constant-rate return to the input-driven
  throttle speed. Tracking **auto-reverts** to normal smoothing the moment speed lands on
  the target, so the mode never leaks into ordinary flight.
- Only transformers that route through `AdvanceSpeed` (base `VesselTransformer`,
  `SingleStickVesselTransformer`) honor tracking; `GunVesselTransformer` /
  `CommandVesselTransformer` have bespoke `MoveShip` paths and are untouched.

## Visual model (SpeedTunnelEffectController, on the Rhino prefab root)

Drive signal = **measured `VesselStatus.Speed`**, not boost state — so the visual matches
the ramp up and the fast return down symmetrically, with nothing to desync.

- `effect01 = InverseLerp(minEffectSpeed, maxEffectSpeed, Speed)`, lightly smoothed
  (`responsiveness`/s) to round off teleports/resets.
- **FOV**: home is captured from the live camera the frame the effect takes over
  (never a settings default — see the home-values rule below), then
  `fov = home − fovDrop × effect01` (floor-clamped 1°). The narrowing magnifies the frame
  centre — telephoto push-in.
- **Panini**: `PostProcessingManager.SetSpeedTunnelPanini(−paniniDrop × effect01)` — a
  *signed offset* from the profile's own Panini state, captured as home the first time
  the effect touches it (the GamePlay profile ships a shared baseline of **0.7**; the
  tunnel relaxes it toward rectilinear). At 0 the exact home distance + active flag are
  restored. The override lives on the volume's **instantiated** profile
  (`Volume.profile`), so the profile asset is never mutated; the cache revalidates across
  the ortho/perspective profile swap.
- **Gating**: local human pilot only (`IsLocalUser && !IsInitializedAsAI`, and only once
  `Player` is set — those are `IVesselStatus` default members routed through `Player`).
  Remote/AI Rhinos never touch the local camera or the global volume. The effect tracks
  which camera it pushed and restores that one if the active camera changes mid-effect
  (end cam, death cam).

**Home-values rule (do not regress):** the effect starts from whatever FOV/Panini the
game is ACTUALLY running with and returns there exactly. Never anchor to a constant or a
settings default — that reads as a snap onto foreign values the moment the boost engages.

## Tuning knobs

| Knob | Where | Shipped value |
|---|---|---|
| `maxBoostMultiplier` | `_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset` | 6 (~310 top speed) |
| `accelerationPerSecond` | same asset | 70 (top in ~3.6s) |
| `returnPerSecond` | same asset | 500 (~0.5s return) |
| `engageSFX` | same asset | BoostActivate (13) |
| `minEffectSpeed` / `maxEffectSpeed` | `SpeedTunnelEffectController` on Rhino prefab | 70 / 280 |
| `fovDrop` | same component | 25° below home at full effect |
| `paniniDrop` | same component | 0.5 below the 0.7 baseline |
| `responsiveness` | same component | 12/s |

## In-editor verification

1. Launch any game mode as the Rhino (or menu freestyle) with a gamepad, touch, or
   keyboard+mouse. Fly full throttle and straight.
2. Speed should climb **linearly** (no ease-in curve) toward ~6× cruise over ~3.6s; one
   BoostActivate SFX on engage; the view should progressively narrow (zoom-in) while the
   fisheye-ish Panini compression relaxes — tunnel vision proportional to speed.
3. Break the line: speed returns to input speed in ~0.5s and the view relaxes back in
   lockstep. **Confirm FOV and Panini land exactly on pre-boost values** (compare against
   a non-Rhino vessel side by side).
4. Wobble in and out of the straight line rapidly — no snapping to foreign FOV/Panini
   values at any point (the home-values rule).
5. Multiplayer sanity: a second client's Rhino boosting must not change YOUR camera or
   post-processing.
6. End a turn mid-boost: boost state clears, effect returns home.

## Follow-ups

- Menu freestyle uses Cinemachine, so only the Panini half of the effect applies there
  (no `CustomCameraController` to drive FOV on). Extend to the menu vCam if freestyle
  parity matters.
- The effect window (70–280) is hand-tuned against the default throttle constants
  (`DefaultThrottleScaler` 50, `MinimumSpeed` 10). Modes that rescale throttle would need
  the window retuned — or derived from `ComputeThrottleTarget` if that ever becomes a
  maintenance burden.
- Engage SFX plays on every peer for remote Rhinos (pre-existing `BoostActivate`
  semantics, unchanged).
