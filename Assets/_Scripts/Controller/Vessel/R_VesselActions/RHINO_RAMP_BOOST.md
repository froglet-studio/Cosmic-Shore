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
  tracking switches to `returnPerSecond` — a constant-rate return to the input-driven throttle
  speed. Tracking **auto-reverts** to normal smoothing the moment speed lands on the target, so
  the mode never leaks into ordinary flight.
- Only transformers that route through `AdvanceSpeed` (base `VesselTransformer`,
  `SingleStickVesselTransformer`) honor tracking; `GunVesselTransformer` /
  `CommandVesselTransformer` have bespoke `MoveShip` paths and are untouched.

### The effective numbers (derive these; do not read the asset alone)

`ComputeThrottleTarget` is `XDiff × ThrottleScaler × ThrottleScalerMultiplier ×
CurrentBoostAmount() + MinimumSpeed`, and the Rhino authors `DefaultThrottleScaler 50` /
`DefaultMinimumSpeed 10` on its prefab. The minimum is added **after** the multiply, so:

| quantity | expression | value |
|---|---|---|
| cruise (full throttle, no boost) | `50 × 1 + 10` | **60 u/s** |
| top (ramp engaged) | `50 × 18 + 10` | **910 u/s** |
| build time cruise → top | `(910 − 60) / 140` | **6.1 s** |
| coast time top → cruise | `(910 − 60) / 120` | **7.1 s** |

`ThrottleScalerMultiplier` is disabled on the Rhino and its map's Time entry is an open design
slot pinned to 1, so `CurrentBoostAmount()` is exactly `maxBoostMultiplier` — no elemental
factor and no squaring (the Dolphin's `IsChargedBoostDischarging` path does not apply here).
`VesselTransformer.MaxBoostMultiplier` (5) does **not** clamp this: it is read only by the
`boostChanged` payload and by `DecayBoost`, and the Rhino authors `decayBoost: 0`.

### Speed and turn authority are ONE formula — that is why the top speed could be raised

`MaxTurnRateDegreesPerSecond = speed × RotationThrottleScaler + min(Pitch, Yaw)`, and
`MinTurnRadius = v / ω`. With `RotationThrottleScaler` at **0** — where the Rhino sat until this
pass — ω is a flat 90 °/s, so the turn radius grows **linearly and without bound** with speed: 38 u
at cruise, 197 u at the old ×6 top, and it would have been 579 u at ×18. Raising the top speed
alone would have made the vessel *less* fun, not more, which is the trap: the ramp boost was
already the least usable ability in the fleet because using it cost you the ability to steer.

The Rhino now authors `RotationThrottleScaler: 0.4` (the same dial the Manta runs at 0.2 and the
Serpent at 0.4), which makes the radius **converge** instead of diverge:

    R(v) = 180·v / (π·(v·r + 90))     →     R(∞) = 180/(π·r) = 143.2 u at r = 0.4

| speed | ω | min turn radius |
|---|---|---|
| 60 (cruise) | 114 °/s | 30 u |
| 310 (the old ×6 top) | 214 °/s | 83 u |
| 910 (the new top) | 454 °/s | **115 u** |
| ∞ | — | **143 u** (asymptote) |

So the Rhino at 910 u/s turns inside a tighter circle than it used to at 310, and tripling its
speed again would cost it only another 25 %. **It is the one vessel in the fleet whose agility
grows with speed**, and that — not the top speed on its own — is the identity `GameModes.Headlong`
is built to showcase.

### The number the course is sized to: the FLAT-OUT radius

The gesture that engages the ramp requires `(1 − XDiff) + |YDiff| + |YSum| + |XSum| < 0.3`
(the input strategies' `PerformSpeedAndDirectionalEffects`), so a pilot holding the boost may
spend at most ~0.3 of total stick deflection. Turn rate is linear in stick, so the tightest
circle a pilot can fly **without dropping the boost** is

    R_flat-out(v) = R(v) / stickBudget      ≈ 115 / 0.28 = **410 u** at top speed
                                            (511 u at the asymptote)

That is the real design constant, and it is what makes a corner a decision rather than a chore:
take it at 410 u and keep 910 u/s, or brake into it, turn hard, and pay 6.1 s to wind back up.

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
| `maxBoostMultiplier` | `_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset` | **18** (910 top speed) |
| `accelerationPerSecond` | same asset | **140** (top in 6.1s) |
| `returnPerSecond` | same asset | **120** (7.1s coast back to cruise) |
| `engageSFX` | same asset | BoostActivate (13) |
| `RotationThrottleScaler` | `_Prefabs/Spacevessels/Rhino.prefab` | **0.4** — the turn-radius asymptote, `180/(pi*r)` = 143 u |
| `followOffset.z` | `_SO_Assets/Camera/RhinoCameraSettingsSO.asset` | **-70** (was -120) |

`returnPerSecond` is the **feel** dial, not a safety one: at 120 the speed bleeds over seconds
instead of falling off a cliff, so speed carried into a corner is speed you still have coming out
of it. At the previous 500 the ramp dumped 310 -> 60 in half a second, which meant a boost was
worth nothing the instant you touched the stick and there was no reason to build one.

The camera moved because `followOffset.z` was **-120** against the Squirrel's -17 and the
Sparrow's -50: at that distance the hull is small in frame and both its speed and its 90 deg/s
rotation read as nothing. -70 still frames the 30-120 unit blade (`ShieldSkimmerScaleConfig`,
`baseScale 30` / `maxScale 120`) while roughly doubling the apparent speed. Note this also halves
the tail width, which is derived (`|followOffset.z| / 20`, `Docs/VESSEL_TAIL_AND_JETS.md`) and
wants an eye at playtest. `dynamicMaxDistance` and `adaptiveMaxDistance` on that asset are **dead
dials** - `adaptiveMaxDistance` is read by nothing outside the inspector, and the dynamic pair
apply only on the `mode != 0` branch of `CustomCameraController.ApplySettings`, which this vessel
does not take.

The tunnel's own knobs are fleet-wide and live in `Resources/SpeedTunnelConfig.asset` — see
`Docs/SPEED_TUNNEL.md` §3. They are deliberately NOT listed here: a copy in a per-vessel doc is
how a platform law starts reading like a vessel feature again.

## In-editor verification

1. Launch any game mode as the Rhino (or menu freestyle) with a gamepad, touch, or
   keyboard+mouse. Fly full throttle and straight.
2. Speed should climb **linearly** (no ease-in curve) toward **910** over **~6s**; one
   BoostActivate SFX on engage; the view should progressively narrow (zoom-in) while the
   fisheye-ish Panini compression relaxes — tunnel vision proportional to speed. **The tunnel
   saturates at 280 u/s** (`Resources/SpeedTunnelConfig`, `maxEffectSpeed`), so everything above
   that looks optically identical — 910 does not read as three times 310 through the lens, only
   through the world going past. That is a fleet-wide absolute law and is deliberately NOT
   retuned for this vessel; four other hulls already exceed the ceiling.
3. Break the line: speed **coasts** back to input speed over ~7s and the view relaxes with it.
   **Confirm FOV and Panini land exactly on pre-boost values.** (Note: every
   vessel tunnels now, so there is no longer a tunnel-free vessel to compare against —
   judge the return against the Rhino's own resting view.)
3b. **Steer at top speed.** At 910 u/s full stick should sweep the nose at ~454 °/s and put the
   vessel through a ~115-unit circle. Compare against the same manoeuvre at cruise: the CIRCLE
   should be bigger, but only ~4x for a 15x speed increase. If it feels twitchy rather than
   authoritative, `RotationThrottleScaler` is the dial (and it moves the asymptote with it).
4. Wobble in and out of the straight line rapidly — no snapping to foreign FOV/Panini
   values at any point (the home-values rule).
5. Multiplayer sanity: a second client's Rhino boosting must not change YOUR camera or
   post-processing.
6. End a turn mid-boost: boost state clears, effect returns home.

## Blast radius — the Rhino is MANDATORY in three modes

Every number above is a property of the VESSEL, so it lands in every mode that flies it, and the
Rhino is the required hull in **Astro League (37)**, **Peel the Cage (39)** and now
**Headlong (48)**. Both existing modes want a playtest against this pass, and neither was
retuned here:

- **Astro League** — the court IS the cell's nucleus, and a pilot who can cross it at 910 u/s
  arrives at the ball far faster than the mode was tuned for. `SkimmerSwingKinematics` composes
  the vessel's velocity into the blade's strike speed, so a boosted swing hits very much harder
  than before. Watch for the ball leaving the court in one touch.
- **Peel the Cage** — the cage's outer radius is 360 u and its innermost shell is 100 u. The
  Rhino's boosted turn radius (115 u) is now *just* inside the core, but the ramp needs ~5,500 u
  of straight line to reach top speed and the arena is 720 u across, so in practice the boost
  cannot wind past roughly ×4 in there. That is a self-limiting arena rather than a fix.

## Follow-ups

Tunnel-side follow-ups (menu Cinemachine, window tuning) moved to `Docs/SPEED_TUNNEL.md` §6
with the rest of the law.

- Engage SFX plays on every peer for remote Rhinos (pre-existing `BoostActivate`
  semantics, unchanged).
- **The gesture threshold (0.3) is shared code**, in every `IInputStrategy`'s
  `PerformSpeedAndDirectionalEffects`. The Rhino is the only vessel that binds
  `FullSpeedStraightAction` (verified across all 11 prefabs) and nothing binds
  `MinimumSpeedStraightAction` at all, so widening it is currently a Rhino-only change in
  practice — but it is not one in principle, and it was left alone this pass. If the flat-out
  line proves too fussy to hold, that threshold is the honest lever, and it must move in all
  five strategies together.
- **Three of the four element slots are still open design** (`Resources/ElementalAbilityMaps/Rhino.asset`
  authors only Mass → trail slab size), so three of the Rhino's four HUD cards render LOCKED and
  nothing in this pass scales with an element. Proposals are in
  `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 "Rhino — Bulldozer"; none of them touch speed or
  handling, so a future pass may want a Time → acceleration / Space → turn-authority re-cut
  instead. Deliberately out of scope here.
