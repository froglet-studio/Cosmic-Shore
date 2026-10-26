# Rhino Ramp Boost + Speed-Tunnel Quasi Dolly Zoom

> **2026-10-16:** every peer now grades the ramp from `InputStatus`' replicated sticks (replicas
> used to hold `IsBoosting` + ×16.8 forever after the first ramp); Time is read live; climb/bleed
> compares against `VesselTransformer.TrackedSpeed`. The AI now ramps (`RampBoostAIPolicySO`).
> The Time row's L5 is **Momentum**, and its description states the bulldozer-ignores-mass
> identity. `GrowTrailAction` is deleted.

The Rhino's full-speed-straight reward. Holding full throttle with no rotation input
raises `InputEvents.FullSpeedStraightAction` (from the input strategies' deviation
threshold), which the Rhino maps to `RhinoRampBoostAction` (it also fired `GrowTrailAction`
until 2026-10-16, when Trail Slabs / GrowTrail was deleted — its grow loop never ran). The boost
is a **constant-acceleration ramp**, and it is sold optically by an **inverse quasi dolly
zoom** — field of view and Panini projection start at the same home values every vessel
runs with and drop below them proportionally to live speed.

## Speed model

- **Engage** (`RampBoostActionSO` → `RampBoostActionExecutor.Begin`): `IsBoosting` on,
  `BoostMultiplier` = `maxBoostMultiplier` (top-speed target), and the transformer enters
  **constant-rate speed tracking** — `VesselTransformer.SetSpeedTrackingRate(accelerationPerSecond)`
  makes `AdvanceSpeed` use `MoveTowards` (linear, steady slope) instead of the default
  exponential lerp. The TIME elemental multiplies the acceleration (Element → parameter,
  read LIVE every climb frame since 2026-10-16 — it used to be snapshotted at first engage, so a
  Time level earned mid-ramp changed nothing until the ramp fell off). **Time does NOT raise the
  top speed**: the transformer's per-hull `BoostSpeedMultiplier` is not authored on the Rhino
  (see "The effective numbers"). *(This line used to say Time "separately multiplies the top
  speed inside the transformer as for any boost" — stale.)*
- **Graded hold** (`RampBoostActionExecutor.Update`, every frame while armed): the executor
  re-reads `StraightLineGesture.DeviationFromFullSpeedStraight` and scales itself by it —
  `BoostMultiplier = lerp(1, maxBoostMultiplier, straightness)`, full across the gesture's own
  0.30 plateau and falling linearly to plain cruise at `straightnessGraceBand`. Tracking is set
  to `accelerationPerSecond` while the target is above the current speed and `bleedPerSecond`
  while it is below (asked of `VesselTransformer.CurrentThrottleTarget`, never re-derived, and
  compared against `VesselTransformer.TrackedSpeed` — the transformer's own tracked speed, not
  the published `VesselStatus.Speed`, which is after the impact slow, warp field and flight time
  scale and so could pick the climb rate for a bleed).
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
`DefaultMinimumSpeed` **0** on its prefab. The minimum is added **after** the multiply, so:

| quantity | expression | value |
|---|---|---|
| cruise (full throttle, no boost) | `50 × 1 + 0` | **50 u/s** |
| top (ramp engaged, dead straight) | `50 × 16.8 + 0` | **840 u/s** |
| build time cruise → top | `(840 − 50) / 220` | **3.6 s** |
| coast time top → cruise (disengaged) | `(840 − 50) / 120` | **6.6 s** |

> **RETUNED 2026-09-25 — top speed to 70%, speed-driven turning to 40%.** `maxBoostMultiplier`
> 24 → **16.8** (top 1200 → **840 u/s**; cruise untouched at 50) and `Rhino.prefab`'s
> `RotationThrottleScaler` 0.5 → **0.2** (the term that makes turning grow with speed). At top
> speed ω is now `840 × 0.2 + 90` = **258 °/s** (was 690), the minimum radius **186.5 u** (was
> 99.6), the asymptote `180/(π·0.2)` = **286.5 u** (was 114.6) and the flat-out radius
> **666.2 u** (was 355.9). Cruise radius barely moves (24.9 → 28.6 u). The tables further down
> this document (the convergence table, the stick/speed/radius table and every Headlong figure
> quoted from them) were derived at the OLD numbers and are kept as the record of that tuning;
> the live curve is:
>
> | stick | sustained speed | radius it holds | ×flat-out |
> |---|---|---|---|
> | 0.30 (plateau edge) | 840 u/s | 622 u | 0.93 |
> | 0.40 | 727 | 442 | 0.66 |
> | 0.50 | 614 | 331 | 0.50 |
> | 0.60 | 501 | 252 | 0.38 |
> | 0.70 | 389 | 190 | 0.28 |
> | 0.80 | 276 | 136 | 0.20 |
> | 0.90 | 163 | 85 | 0.13 |
> | 1.00 | 50 | 29 | 0.04 |
>
> The agility-grows-with-speed identity survives (the radius still converges) but is much
> weaker: a 10× speed increase now costs 85 u of radius rather than 13. Consequences, measured
> by compiling and running the shipped `HeadlongCircuit` + `HeadlongCircuitTests` offline
> (400 seeds × 4 intensities): every Headlong level got HARDER — median corners that cost speed
> 1/2/2/3 → **2/4/4/5**, median hardest corner 99/80/50/37% of top → **92/66/47/34%** — because
> the cell shell is fixed while the flat-out circle nearly doubled. Level 1's safety floor went
> `CornerRadiusFactor` 0.62 → **0.75** to keep its "never a hairpin" promise; levels 2-4 are
> unchanged and still ordered. Regatta and Broadside `--check` are unchanged (the Rhino carries
> no Time handicap row on either card).
| bleed while still steering | `bleedPerSecond` | **300 u/s** |

> **`DefaultMinimumSpeed` was 10 until the minimum-throttle brake** (`MinimumThrottleBrake`,
> `SQUIRREL_DRIFT.md` §3.5). A floor is a speed the pilot cannot give back, so a two-thumb flier
> that is meant to be able to STOP cannot author one — and all that 10 ever did here was pad both
> ends of the ladder by 10 u/s. Measured over 1,600 generated circuits (400 seeds × 4 intensities)
> the Headlong courses are **bit-identical** before and after, because the corner safety floor it
> feeds (`CornerRadiusFactor × FlatOutRadius`, 356.3 → 355.9) never binds. What moved is 10 u/s
> off every speed in the tables below.

`CurrentBoostAmount()` is `BoostMultiplier × BoostSpeedMultiplier` (`VesselTransformer.cs`), and
the Rhino does not author `BoostSpeedMultiplier` (the class default is a constant 1); the separate
`ThrottleScalerMultiplier`, which multiplies the throttle term outside the boost, is authored
**disabled** on `Rhino.prefab`. So the boost factor is exactly `maxBoostMultiplier` — no
elemental factor and no squaring. *(This paragraph used to name `ThrottleScalerMultiplier` as the
factor inside `CurrentBoostAmount()`; the in-boost factor is `BoostSpeedMultiplier`.)* (the Dolphin's
`IsChargedBoostDischarging` path does not apply here). **TIME does not touch that ceiling; it
scales `accelerationPerSecond`**, so the build times above are the resting ones — at Time 10 the
ramp climbs at 550/s and reaches top in **~1.4 s** (`790 / 550`), at the deficit floor (×0.5) it
climbs at 110/s and takes **~7.2 s** (`790 / 110`). *(These were 2.1 s / 10.5 s against the
pre-2026-09-25 top of 1200.)* The executor has always scaled by Time here (today through `RampBoostActionSO.timeAccelerationMultiplier`, an `ElementalFloat` on the asset); the map entry behind it
was an `(open design slot)` authored 1.0/1.0 until Broadside's first playtest filled it
(**Ramp Spool**, 2.5 / 0.5 — `Assets/_Scripts/Controller/Arcade/BROADSIDE.md`). Since 2026-10-16
the row's description also states the hull's identity outright — **the bulldozer ignores mass**:
the hull carries no `VesselChangeSpeedByPrismEffect`, the plough shoves prisms aside
(`RHINO_PLOUGH.md`) and the ramp keeps climbing. That is a design statement now, not a gap. Its
L5 is **Momentum**: above 70% of top speed (588 of 840 u/s) danger prisms cannot drain the Rhino
(`VesselElementalImmunity`'s `WhileAboveTopSpeedFraction` window, DangerPrism-scoped; the
executor is the hull's `ITopSpeedSource`, `TopSpeed` = (0 + 50) × 16.8). The HUD's Time card is
lit while the ramp is boosting, flashes once when Momentum takes hold, and its gauge fills with
speed / top speed (2026-10-17; it used to light only while Momentum held). *A capability
live in code and flat in data reads exactly like a capability that does not exist* — three
separate documents recorded "Time reaches nothing on the Rhino" as a measurement while this line
sat above the multiply.
`VesselTransformer.MaxBoostMultiplier` (5) does **not** clamp this: it is read only by the
`boostChanged` payload and by `DecayBoost`, and the Rhino authors `decayBoost: 0`.

### Speed and turn authority are ONE formula — that is why the top speed could be raised

`MaxTurnRateDegreesPerSecond = speed × RotationThrottleScaler + min(Pitch, Yaw)`, and
`MinTurnRadius = v / ω`. With `RotationThrottleScaler` at **0** — where the Rhino sat until this
pass — ω is a flat 90 °/s, so the turn radius grows **linearly and without bound** with speed: 38 u
at cruise, 197 u at the old ×6 top, and it would have been 579 u at ×18. Raising the top speed
alone would have made the vessel *less* fun, not more, which is the trap: the ramp boost was
already the least usable ability in the fleet because using it cost you the ability to steer.

The Rhino authored `RotationThrottleScaler: 0.5` at this pass (**0.2 since 2026-09-25** — see the
retune box above; the numbers in this section are the 0.5 record) (against the Manta's 0.2 and the Serpent's
0.4), which makes the radius **converge** instead of diverge:

    R(v) = 180·v / (π·(v·r + 90))     →     R(∞) = 180/(π·r) = 114.6 u at r = 0.5

| speed | ω | min turn radius |
|---|---|---|
| 50 (cruise) | 115 °/s | 25 u |
| 310 (the original ×6 top) | 245 °/s | 73 u |
| 1200 (the new top) | 690 °/s | **100 u** |
| ∞ | — | **115 u** (asymptote) |

So the Rhino at 1200 u/s turns inside a tighter circle than it used to at 310, and tripling its
speed again would cost it only another 15 %. **It is the one vessel in the fleet whose agility
grows with speed**, and that — not the top speed on its own — is the identity
`GameModes.Headlong` is built to showcase.

### The number the course is sized to: the FLAT-OUT radius

The gesture that engages the ramp requires `(1 − XDiff) + |YDiff| + |YSum| + |XSum| < 0.3`
(`StraightLineGesture.EngageThreshold`), so a pilot holding the ramp at FULL power may spend at
most ~0.3 of total stick deflection. Turn rate is linear in stick, so the tightest circle they
can fly **without giving any of it up** is

    R_flat-out(v) = R(v) / stickBudget      ≈ 99.6 / 0.28 = **356 u** at top speed
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
| 0.30 (plateau edge) | 1200 u/s | 332 u | 0.93 |
| 0.40 | 1036 | 244 | 0.69 |
| 0.50 | 871 | 190 | 0.53 |
| 0.60 | 707 | 152 | 0.43 |
| 0.70 | 543 | 123 | 0.35 |
| 0.80 | 379 | 97 | 0.27 |
| 0.90 | 214 | 69 | 0.19 |
| 1.00 (hard over) | 50 (cruise) | 25 | 0.07 |

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
- **Every peer grades, from the same sticks (2026-10-16).** Press/release round-trip through
  `R_VesselActionHandler`, so the executor is live on every peer, and the sticks it grades by are
  `InputStatus`' owner-write NetworkVariables — the owner's fingers, or an AI's writes on the
  server, read identically everywhere. *Until 2026-10-16 only the owner graded
  (`IPlayer.IsNetworkOwner`) and a replica kept the full multiplier "while armed" — but a graded
  ramp is never disarmed by an input event (the release at the engage threshold is the top of
  the slope), only by leaving the grace band in `Tick`, so every replica held `IsBoosting` and
  ×16.8 for the rest of the turn after the first ramp, and every per-peer reader of either
  (immunity windows, telemetry, the roll controller) read a boost that had ended.*

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
| `maxBoostMultiplier` | `_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset` | **16.8** (840 top speed; 24 / 1200 until 2026-09-25) |
| `accelerationPerSecond` | same asset | **220** at Time rest (cruise → top in 3.6 s); × `timeAccelerationMultiplier` (Time, ×1 at rest → **×2.5 at level 10**, floored **×0.5**) → 110 .. 220 .. 550 /s, read live |
| `straightnessGraceBand` | same asset | **1.0** — hard-over stick = plain cruise; `0.3` restores the binary latch |
| `bleedPerSecond` | same asset | **300** — tracking down to the target a steering pilot chose |
| `returnPerSecond` | same asset | **120** (6.6 s coast back to cruise once disengaged) |
| `engageSFX` | same asset | BoostActivate (13) |
| `RotationThrottleScaler` | `_Prefabs/Spacevessels/Rhino.prefab` | **0.2** (0.5 until 2026-09-25) — the turn-radius asymptote, `180/(pi*r)` = 286 u |
| AI arming | `RampBoostAIPolicySO` → `RhinoAIBoostPolicy.asset` on the Rhino's AIPilot | ramp on a straight that pays back, brake ON the ramp to a corner speed, let go at a sharp corner (`Controller/AI/AI_BOOST.md`, Ramp) — first AI ramp 2026-10-14/16, rebuilt 2026-10-17 |
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
2. Speed should climb **linearly** (no ease-in curve) toward **840** over **~3.6s**; one
   BoostActivate SFX on engage; the view should progressively narrow (zoom-in) while the
   fisheye-ish Panini compression relaxes — tunnel vision proportional to speed. **The tunnel
   saturates at 280 u/s** (`Resources/SpeedTunnelConfig`, `maxEffectSpeed`), so everything above
   that looks optically identical — 840 does not read as three times 280 through the lens, only
   through the world going past. That is a fleet-wide absolute law and is deliberately NOT
   retuned for this vessel; four other hulls already exceed the ceiling.
3. Break the line HARD (stick to the stop): the ramp disengages and speed **coasts** back to
   input speed over ~6.6s, the view relaxing with it.
   **Confirm FOV and Panini land exactly on pre-boost values.** (Note: every
   vessel tunnels now, so there is no longer a tunnel-free vessel to compare against —
   judge the return against the Rhino's own resting view.)
3a. **The graded ramp is the thing to judge, and it is the whole point of this pass.** Hold the
   ramp, then feed in a LITTLE stick and hold it. Speed should settle at a lower cruise rather
   than collapsing — roughly 727 u/s at 0.4 stick, 614 at a half, 389 at 0.7 (the live table in
   the retune box above) — and the
   vessel should carve a correspondingly tighter arc. Sweeping the stick slowly from centre to
   the stop should read as one continuous trade, with no step anywhere, and hard-over should
   land on plain cruise (50 u/s) exactly as disengaging does.
3b. **Steer at top speed.** At 840 u/s full stick should sweep the nose at ~258 °/s and put the
   vessel through a ~186-unit circle. Compare against the same manoeuvre at cruise (~29 u): the
   CIRCLE should be bigger, ~6.5x for a 16.8x speed increase (it was ~3.5x for 20x at the old
   0.5 scaler). If it feels twitchy rather than
   authoritative, `RotationThrottleScaler` is the dial (and it moves the asymptote with it).
3c. **AI Rhino.** Add one and watch it fly — it should now RAMP on straights that pay the
   wind-up back, brake on the ramp into each ring and let go at sharp corners (`RampBoostAIPolicySO`;
   `Controller/AI/AI_BOOST.md`, Ramp). *Until then the AI never ramped at all:
   the full-speed-straight gesture is raised only by a human input strategy, so this step's old
   wording ("an AI should also bleed speed into its turns") described a ramp the AI never had.*
3d. **Peers (MPPM two clients).** Ramp on A, then steer hard: B's copy of A must bleed too and
   drop `IsBoosting` when A leaves the grace band (replicas used to hold ×16.8 for the turn).
4. Wobble in and out of the straight line rapidly — no snapping to foreign FOV/Panini
   values at any point (the home-values rule).
5. Multiplayer sanity: a second client's Rhino boosting must not change YOUR camera or
   post-processing.
6. End a turn mid-boost: boost state clears, effect returns home.

## Blast radius — the Rhino is MANDATORY in three modes

Every number above is a property of the VESSEL, so it lands in every mode that flies it, and the
Rhino is the required hull in **Astro League (37)**, **Cleave (39)** and now
**Headlong (49)**. Both existing modes want a playtest against this pass, and neither was
retuned here:

- **Astro League** — the court IS the cell's nucleus, and a pilot who can cross it at 840 u/s
  (1200 when this was written) arrives at the ball far faster than the mode was tuned for.
  `SkimmerSwingKinematics` composes the vessel's velocity AND its angular rate into the blade's
  strike speed; this pass raised BOTH (`RotationThrottleScaler` 0.4 → 0.5), and the 2026-09-25
  retune pulled both back (0.2, 840), so a boosted swing hits harder than at the original ×6 but
  less than this pass first shipped. Watch for the ball leaving the court in one
  touch. The graded ramp cuts the other way — a pilot turning toward the ball no longer carries
  full ramp into the strike — so this needs a play test rather than a prediction.
- **Cleave** — the cage's outer radius is 360 u and its innermost shell is 100 u. The
  Rhino's boosted minimum turn radius is now ~186 u (100 u when this was written), and the ramp
  needs ~1,600 u of straight line to reach top speed at rest (`(840² − 50²) / (2 × 220)`) while
  the arena is 720 u across, so a full-diameter straight winds it to only ~565 u/s (≈×11). That
  is a self-limiting arena rather than a fix.
- **All three** want a playtest at the 2026-10-16 kit too: the AI now ramps (it raced every
  Rhino-only mode at cruise before), and Momentum + the Mass afterimages change what a fast
  Rhino leaves behind and what stops it.

## Follow-ups

Tunnel-side follow-ups (menu Cinemachine, window tuning) moved to `Docs/SPEED_TUNNEL.md` §6
with the rest of the law.

- **OPEN:** engage SFX plays on every peer for remote Rhinos (pre-existing `BoostActivate`
  semantics, unchanged). An AI re-arm also replays it (`minRearmSeconds` 0.5 spaces them).
- **The gesture threshold (0.3) is shared code** and now has ONE home,
  `StraightLineGesture.EngageThreshold`. The Rhino is the only vessel that binds
  `FullSpeedStraightAction` (verified across all 11 prefabs) and nothing binds
  `MinimumSpeedStraightAction` at all, so moving it is currently a Rhino-only change in
  practice — but it is not one in principle. Since the ramp became graded it is also no longer
  the fussy edge it was: it is the top of a slope rather than a cliff, so a pilot who drifts past
  it loses a little speed instead of all of it. Prefer `straightnessGraceBand` as the feel dial.
- **OPEN: `MinimumSpeedStraightAction` is raised by the input strategies and bound by nothing**
  (re-checked 2026-10-16). It is
  the mirror of the gesture this ability rides and would be the natural home for a Rhino
  brake/anchor if one is ever wanted.
- ~~**Two of the four element slots are still open design** … two HUD cards render LOCKED and no
  L5 upgrade is authored on any row.~~ **Stale since 2026-10-16:** `Resources/ElementalAbilityMaps/Rhino.asset`
  now authors all four rows with scaling and an L5 each (Charge — Sword Energy / Live Wire; Mass —
  Afterimage / Danger Afterimages; Space — Blade Reach / Breaker; Time — Ramp Spool / Momentum),
  and the row binds 4/4 with PLACEHOLDER icons — no card is LOCKED. The Time row's own `Input` is
  still deliberately **0**: the ramp engages on a full-throttle straight, not a button.
- **DONE 2026-10-26 (Garrett: "yes"): the Time gauge DIMS as the line bends** while the ramp is engaged - `RhinoHUDController.PushLineToTimeGauge` scales the gauge's authored alpha from 1 (straight) down to `bentLineGaugeAlpha` (0.3) by `RampBoostActionExecutor.Straightness01`; the fill is still speed / top speed. *Was:* **OPEN: the graded ramp is not reflected on the HUD.** `RampBoostActionExecutor.Straightness01`
  is published for exactly this and is still read by nothing. The Time card now exists — lit
  while the ramp boosts, flashing once at Momentum, its gauge filling with speed / top speed
  (2026-10-17) — and straightness is still the obvious second readout for it: the one number that tells a
  pilot how much of their boost the current line is costing them, which they can otherwise only
  infer from the speed they are not gaining.
