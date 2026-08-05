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

## Visual model — the speed-tunnel PLATFORM LAW

The optical sell is **no longer part of the Rhino**. The speed tunnel is a fleet-wide platform
law: every vessel's FOV + Panini respond to its own measured speed, from one static driver
bound in `VesselController.Initialize`, with no per-vessel wiring anywhere. Behaviour, the
absolute speed window, the home-values rule, tuning and verification all live in
**`Docs/SPEED_TUNNEL.md`** — that is the single reference; do not re-describe it here.

What that means for this action: the ramp boost wires nothing and knows nothing about the
visual. It raises speed; the law reads speed. Because the drive signal is measured
`VesselStatus.Speed` and not boost state, the tunnel tracks the constant-acceleration ramp UP
and the fast return DOWN symmetrically, with nothing to keep in step. Retuning the ramp's speed
numbers below therefore moves the Rhino's tunnel too — but retuning the *tunnel* moves the whole
fleet, which is the point of it being a law.

## Tuning knobs

| Knob | Where | Shipped value |
|---|---|---|
| `maxBoostMultiplier` | `_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset` | 6 (~310 top speed) |
| `accelerationPerSecond` | same asset | 70 (top in ~3.6s) |
| `returnPerSecond` | same asset | 500 (~0.5s return) |
| `engageSFX` | same asset | BoostActivate (13) |

The tunnel's own knobs are fleet-wide and live in `Resources/SpeedTunnelConfig.asset` — see
`Docs/SPEED_TUNNEL.md` §3. They are deliberately NOT listed here: a copy in a per-vessel doc is
how a platform law starts reading like a vessel feature again.

## In-editor verification

1. Launch any game mode as the Rhino (or menu freestyle) with a gamepad, touch, or
   keyboard+mouse. Fly full throttle and straight.
2. Speed should climb **linearly** (no ease-in curve) toward ~6× cruise over ~3.6s; one
   BoostActivate SFX on engage; the view should progressively narrow (zoom-in) while the
   fisheye-ish Panini compression relaxes — tunnel vision proportional to speed.
3. Break the line: speed returns to input speed in ~0.5s and the view relaxes back in
   lockstep. **Confirm FOV and Panini land exactly on pre-boost values.** (Note: every
   vessel tunnels now, so there is no longer a tunnel-free vessel to compare against —
   judge the return against the Rhino's own resting view.)
4. Wobble in and out of the straight line rapidly — no snapping to foreign FOV/Panini
   values at any point (the home-values rule).
5. Multiplayer sanity: a second client's Rhino boosting must not change YOUR camera or
   post-processing.
6. End a turn mid-boost: boost state clears, effect returns home.

## Follow-ups

Tunnel-side follow-ups (menu Cinemachine, window tuning) moved to `Docs/SPEED_TUNNEL.md` §6
with the rest of the law.

- Engage SFX plays on every peer for remote Rhinos (pre-existing `BoostActivate`
  semantics, unchanged).
