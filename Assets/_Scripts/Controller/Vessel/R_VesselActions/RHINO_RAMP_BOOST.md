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
- **Graded hold** (`RampBoostActionExecutor.Update`, every frame while armed): the executor
  re-reads `StraightLineGesture.DeviationFromFullSpeedStraight` and scales itself by it —
  `BoostMultiplier = lerp(1, maxBoostMultiplier, straightness)`, full across the gesture's own
  0.30 plateau and falling linearly to plain cruise at `straightnessGraceBand`. Tracking is set
  to `accelerationPerSecond` while the target is above the current speed and `bleedPerSecond`
  while it is below (asked of `VesselTransformer.CurrentThrottleTarget`, never re-derived).
  **This is what makes the ability worth practising** — see "The ramp is a slope" below.
- **Release** (grace band exceeded, turn end, or component disable): boost state restores and
  tracking switches to `returnPerSecond` — a constant-rate coast to the input-driven throttle
  speed. Tracking **auto-reverts** to normal smoothing the moment speed lands on the target, so
  the mode never leaks into ordinary flight. The input strategies' release at deviation 0.30 is
  deliberately **not** a disengagement (`StopAction` → `ReleaseGesture`): it is the top of the
  slope, and the graded tick owns the rest of the way down.
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
| top (ramp engaged, dead straight) | `50 × 24 + 10` | **1210 u/s** |
| build time cruise → top | `(1210 − 60) / 220` | **5.2 s** |
| coast time top → cruise (disengaged) | `(1210 − 60) / 120` | **9.6 s** |
| bleed while still steering | `bleedPerSecond` | **300 u/s** |

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

The Rhino now authors `RotationThrottleScaler: 0.5` (against the Manta's 0.2 and the Serpent's
0.4), which makes the radius **converge** instead of diverge:

    R(v) = 180·v / (π·(v·r + 90))     →     R(∞) = 180/(π·r) = 114.6 u at r = 0.5

| speed | ω | min turn radius |
|---|---|---|
| 60 (cruise) | 120 °/s | 29 u |
| 310 (the original ×6 top) | 245 °/s | 73 u |
| 1210 (the new top) | 695 °/s | **100 u** |
| ∞ | — | **115 u** (asymptote) |

So the Rhino at 1210 u/s turns inside a tighter circle than it used to at 310, and tripling its
speed again would cost it only another 15 %. **It is the one vessel in the fleet whose agility
grows with speed**, and that — not the top speed on its own — is the identity
`GameModes.Headlong` is built to showcase.

### The number the course is sized to: the FLAT-OUT radius

The gesture that engages the ramp requires `(1 − XDiff) + |YDiff| + |YSum| + |XSum| < 0.3`
(`StraightLineGesture.EngageThreshold`), so a pilot holding the ramp at FULL power may spend at
most ~0.3 of total stick deflection. Turn rate is linear in stick, so the tightest circle they
can fly **without giving any of it up** is

    R_flat-out(v) = R(v) / stickBudget      ≈ 100 / 0.28 = **356 u** at top speed
                                            (409 u at the asymptote)

That is still the anchor the course ladder is stated in — but since the ramp became graded it is
one point on a curve rather than the whole design.

### The ramp is a SLOPE, not a switch — and that is the whole skill ceiling

The ability shipped as a **binary latch**: full power inside the gesture, nothing at all outside
it. Two operating points is one decision — *is this corner wider than 356 u or not?* — so a
corner was a classification, every lap was the same lap, and the play-test verdict was that
there was nothing here to master.

`RampBoostActionSO` now LERPS its multiplier down across a grace band instead of dropping it:

    straightness = 1 − clamp01((deviation − 0.30) / (graceBand − 0.30))
    multiplier   = lerp(1, maxBoostMultiplier, straightness)

Nothing else changed, and everything a pilot can now practise falls out of composing that one
lerp with two formulas that were already there — turn rate is linear in stick, and ω grows with
speed. For a single-axis turn at full throttle the gesture's deviation **is** the stick fraction,
so the composition is exact:

| stick | sustained speed | radius it holds | ×flat-out |
|---|---|---|---|
| 0.30 (plateau edge) | 1210 u/s | 332 u | 0.93 |
| 0.40 | 1046 | 244 | 0.69 |
| 0.50 | 881 | 190 | 0.53 |
| 0.60 | 717 | 153 | 0.43 |
| 0.70 | 553 | 124 | 0.35 |
| 0.80 | 389 | 98 | 0.27 |
| 0.90 | 224 | 71 | 0.20 |
| 1.00 (hard over) | 60 (cruise) | 29 | 0.08 |

A corner is now a continuous optimisation with a real optimum: **the largest speed whose radius
fits**, which differs per corner and per entry speed, and trades against how long the following
straight is (a tighter line is quicker *through* the corner and costs seconds of ramp on the way
out). That trade is the mode. `HeadlongCircuitSettings.SpeedAtStick` / `CornerRadiusAtStick` /
`FastestSpeedForCorner` are this table as code, and `RhinoRampGradingTests` asserts the two
copies agree.

Three details that look arbitrary and are not:

- **The lerp floor is 1, not the prefab's resting `boostMultiplier`.** `CurrentBoostAmount`
  returns 1 while `IsBoosting` is false, so ending at 1 makes the hand-off to disengagement
  seamless; ending at 4 would drop a 150 u/s step at the band edge.
- **Two down-rates.** `bleedPerSecond` (300) is how fast speed tracks to the lower target a
  steering pilot just chose — brisk, because a corner lasts a fraction of a second and at the
  coast rate steering would cost nothing inside one. `returnPerSecond` (120) is the long coast
  after the ramp disengages entirely: speed as a resource you spent winding up.
- **Only the machine that drives the input grades.** Press/release round-trip through
  `R_VesselActionHandler`, so the executor is live on every peer, but a remote replica's
  `InputStatus` is never written and would read as hard-over forever. The gate is
  `IPlayer.IsNetworkOwner`, not `IsLocalUser`, because an AI Rhino's input IS driven — on the
  server.

Authoring `straightnessGraceBand` at `0.30` restores the old binary latch exactly, which is the
safety valve if this ever needs to be walked back (`RhinoRampGradingTests` asserts it).

**The deviation formula now has ONE home.** It was six identical copies of the same three lines
and the same `0.3` across every input strategy, which was survivable while nothing measured it
and stopped being survivable the moment the ability's full-power plateau had to end exactly
where the gesture begins. `StraightLineGesture` (`_Scripts/Controller/IO/`) is that home; all six
strategies now call it.

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
| `maxBoostMultiplier` | `_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset` | **24** (1210 top speed) |
| `accelerationPerSecond` | same asset | **220** (cruise → top in 5.2 s) |
| `straightnessGraceBand` | same asset | **1.0** — hard-over stick = plain cruise; `0.3` restores the binary latch |
| `bleedPerSecond` | same asset | **300** — tracking down to the target a steering pilot chose |
| `returnPerSecond` | same asset | **120** (9.6 s coast back to cruise once disengaged) |
| `engageSFX` | same asset | BoostActivate (13) |
| `RotationThrottleScaler` | `_Prefabs/Spacevessels/Rhino.prefab` | **0.5** — the turn-radius asymptote, `180/(pi*r)` = 115 u |
| `followOffset.z` | `_SO_Assets/Camera/RhinoCameraSettingsSO.asset` | **-70** (was -120) |

`returnPerSecond` is the **feel** dial, not a safety one: at 120 the speed bleeds over seconds
instead of falling off a cliff, so speed carried into a corner is speed you still have coming out
of it. At the original 500 the ramp dumped 310 -> 60 in half a second, which meant a boost was
worth nothing the instant you touched the stick and there was no reason to build one.

`accelerationPerSecond` matters more than `maxBoostMultiplier` in practice, and that is worth
knowing before tuning either: Headlong's legs are 330-925 u, which a Rhino crosses in well under
a second, so **the ceiling is almost never reached on a circuit** and what a pilot feels is how
much of it a straight buys them. Raise the ceiling to make long straights pay; raise the
acceleration to make short ones pay.

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
2. Speed should climb **linearly** (no ease-in curve) toward **1210** over **~5.2s**; one
   BoostActivate SFX on engage; the view should progressively narrow (zoom-in) while the
   fisheye-ish Panini compression relaxes — tunnel vision proportional to speed. **The tunnel
   saturates at 280 u/s** (`Resources/SpeedTunnelConfig`, `maxEffectSpeed`), so everything above
   that looks optically identical — 1210 does not read as four times 310 through the lens, only
   through the world going past. That is a fleet-wide absolute law and is deliberately NOT
   retuned for this vessel; four other hulls already exceed the ceiling.
3. Break the line HARD (stick to the stop): the ramp disengages and speed **coasts** back to
   input speed over ~9.6s, the view relaxing with it.
   **Confirm FOV and Panini land exactly on pre-boost values.** (Note: every
   vessel tunnels now, so there is no longer a tunnel-free vessel to compare against —
   judge the return against the Rhino's own resting view.)
3a. **The graded ramp is the thing to judge, and it is the whole point of this pass.** Hold the
   ramp, then feed in a LITTLE stick and hold it. Speed should settle at a lower cruise rather
   than collapsing — roughly 1046 u/s at a quarter stick, 881 at a half, 553 at 0.7 — and the
   vessel should carve a correspondingly tighter arc. Sweeping the stick slowly from centre to
   the stop should read as one continuous trade, with no step anywhere, and hard-over should
   land on plain cruise (60 u/s) exactly as disengaging does.
3b. **Steer at top speed.** At 1210 u/s full stick should sweep the nose at ~695 °/s and put the
   vessel through a ~100-unit circle. Compare against the same manoeuvre at cruise: the CIRCLE
   should be bigger, but only ~3.5x for a 20x speed increase. If it feels twitchy rather than
   authoritative, `RotationThrottleScaler` is the dial (and it moves the asymptote with it).
3c. **AI Rhino.** Add one and watch it fly — its input IS graded (the gate is `IsNetworkOwner`,
   not `IsLocalUser`), so an AI should now also bleed speed into its turns rather than holding
   full ramp through them.
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

- **Astro League** — the court IS the cell's nucleus, and a pilot who can cross it at 1210 u/s
  arrives at the ball far faster than the mode was tuned for. `SkimmerSwingKinematics` composes
  the vessel's velocity AND its angular rate into the blade's strike speed, and this pass raised
  BOTH (`RotationThrottleScaler` 0.4 → 0.5 is +25% on the tip's swing rate at a given speed), so
  a boosted swing hits very much harder than before. Watch for the ball leaving the court in one
  touch. The graded ramp cuts the other way — a pilot turning toward the ball no longer carries
  full ramp into the strike — so this needs a play test rather than a prediction.
- **Peel the Cage** — the cage's outer radius is 360 u and its innermost shell is 100 u. The
  Rhino's boosted turn radius (100 u) now matches the core, but the ramp needs ~5,200 u of
  straight line to reach top speed and the arena is 720 u across, so in practice the boost cannot
  wind past roughly ×5 in there. That is a self-limiting arena rather than a fix.

## Follow-ups

Tunnel-side follow-ups (menu Cinemachine, window tuning) moved to `Docs/SPEED_TUNNEL.md` §6
with the rest of the law.

- Engage SFX plays on every peer for remote Rhinos (pre-existing `BoostActivate`
  semantics, unchanged).
- **The gesture threshold (0.3) is shared code** and now has ONE home,
  `StraightLineGesture.EngageThreshold`. The Rhino is the only vessel that binds
  `FullSpeedStraightAction` (verified across all 11 prefabs) and nothing binds
  `MinimumSpeedStraightAction` at all, so moving it is currently a Rhino-only change in
  practice — but it is not one in principle. Since the ramp became graded it is also no longer
  the fussy edge it was: it is the top of a slope rather than a cliff, so a pilot who drifts past
  it loses a little speed instead of all of it. Prefer `straightnessGraceBand` as the feel dial.
- **`MinimumSpeedStraightAction` is raised by all six strategies and bound by nothing.** It is
  the mirror of the gesture this ability rides and would be the natural home for a Rhino
  brake/anchor if one is ever wanted.
- **Three of the four element slots are still open design** (`Resources/ElementalAbilityMaps/Rhino.asset`
  authors only Mass → trail slab size), so three of the Rhino's four HUD cards render LOCKED and
  nothing in this pass scales with an element. Proposals are in
  `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 "Rhino — Bulldozer"; none of them touch speed or
  handling, so a future pass may want a Time → acceleration / Space → turn-authority re-cut
  instead. Deliberately out of scope here.
- **The graded ramp is not yet reflected on the HUD.** `RampBoostActionExecutor.Straightness01`
  is published for exactly this and is read by nothing: the Rhino renders four LOCKED ability
  cards, so there is no gauge to bind it to. When the Rhino's map is authored, that value is the
  obvious fill for whichever card owns the ramp — it is the one number that tells a pilot how
  much of their boost the current line is costing them, which they can otherwise only infer from
  the speed they are not gaining.
