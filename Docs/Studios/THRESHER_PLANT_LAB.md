# Thresher Plant Lab

A `/labmaker` browser studio for the **Thresher's Time ability**, the LT **Plant**: the ball stops and
becomes a pivot, the ship swings round it, and LT up flings the ship off. The question it puts to the
designer: **how do we make the Plant less jerky and jarring, starting with a quick deceleration
instead of an instant stop?**

- **Live page:** https://claude.ai/artifact/WxkxN942a6hbfos5xALPwp (private to the owner until shared;
  decisions recorded there land in the shared log Claude reads at the start of each round).
- **Source (the page is published from it):** `Docs/Studios/ThresherPlantLab.html`. Open it in Chrome
  to fly it with a gamepad (the claude.ai viewer can block the Gamepad API).
- **Parity gate:** `node Tools/Build/thresher_plant_lab/parity.cjs` (see "Checked").
- **The vessel:** `Assets/_Scripts/Controller/Vessel/R_VesselActions/THRESHER.md` (§4 Lock, §5a camera).

Two chase views fly the **same** stick: **A** is always the shipped Thresher, **B** flies the approach
picked in the Approach tab (it opens on round 2's **Analog triggers**). The Traces tab plots ball speed,
hull turn rate, camera turn rate and B's triggers over the last 7 s. Each view shows its triggers as it
reads them. The Scorecard runs every approach headless. Keys: A/D steer, hold Q (or Left Shift) for LT,
hold E (or Space) for RT. A held key ramps its trigger over 0.35 s; a pad reads the real depth. R
restarts, Tab swaps A/B in Single view. Autopilot (on at load) loops seeded
plant cycles; any input takes over. Slow-mo plays at quarter speed.

---

## Round 2 (2026-10-13), analog triggers

**Input this round** (the decision log was empty; the designer's direction, verbatim in substance):
both triggers analog. LT becomes a **brake** that only stops the ball on a **full pull**, so the stop's
smoothness is in the player's hand. RT becomes the chain's **rigidity**, a range of elasticity the player
controls, with the axis **flipped**: pull for rigid, release for slack. The camera switches to the launch
view **only with the ball stopped and the chain fully rigid**, i.e. both triggers at a full pull.

### What the scheme does (preset **Analog triggers**, dial `anScheme`)

- **LT below a full pull** brakes the ball (`anBrake` 4/s × depth^`anBrakeCurve` 1.5) and hands more of
  the chain's pull to the ship as the ball turns into an anchor (ship's share = the shipped 5.7% + the
  rest × depth^`anAnchor` 2). A half pull already swings the ship round a sliding ball. Towing, braking
  and planting are one continuous range.
- **A full pull** (≥ `anFull` 0.97) stops whatever speed is left along an S-curve peaking at
  `anStopDecel` 450 u/s². A slow squeeze has already shed most of it, so its stop is gentle. The ship's
  hook starts at the same moment, round the stopping ball. Letting go means dropping below `anRelease`
  0.9: hysteresis, so noise at the end of the trigger's travel cannot flicker the stop.
- **RT is the chain's stiffness**: a one-sided spring at rest length whose natural frequency runs from
  `elSlack` 1.2 rad/s (released, a soft bungee) to `elRigid` 30 rad/s (pulled, ~1 u of stretch at full
  spin), on a log scale, with the chain's full 140 u as a stiff limit. There is no winch any more.
  Released, the turns and the spin carry the ball (or a planted ship) out. Pulled, the chain reels it in
  and the spin rises by angular momentum, so the old winch's let-out and crack fall out of the
  elasticity. `elSlew` 2/s limits how fast the chain itself can stiffen (see Found).
- **The camera** cuts to the launch vantage only when the ball has stopped AND the chain's actual
  rigidity and LT are both at a full pull (plus the shipped spin-rate rule). Easing RT off below
  `anRelease` hands it back. Easing LT off arms the aimed launch, as the shipped LT-up does.
- Carried over from round 1: the spectate glide, the 1 500°/s² follow limit and the soft let-go
  (`rlSpinDown` 0.3, `yank` 0; the elastic chain draws the ball up to speed).

The left view (A) keeps the **shipped** controls. With a human at the sticks the same pull means
different things on each side. The autopilot flies the same **intent** on both: "chain tight" is RT
released on A and RT pulled on B.

### Scorecard

`runBatch()`, 6 seeds × 3 flights, 60 Hz, mean of each run's peak. **This round moved the measuring
windows**: the catch window is 1.6 s, the wind-in comes 1.8 s after the press, and the release 2.7–3.7 s
after it, so a feathered pull's stop falls inside the catch window. Every row was re-measured on the new
windows; round 1's table below used the old ones. Analog rows name the pilot: **squeeze** (LT 0 → 1 in
0.5 s, RT changes over 0.35 s), **slam** (both in one frame), **feather** (LT at half for 0.8 s, then
full over 0.3 s).

| Approach | Ball stop (u/s/frame) | Ball yank (u/s/frame) | Hull jolt, catch (°/s²) | Hull jolt, leave (°/s²) | Hull jolt, wind-in (°/s²) | Camera jolt (°/s²) | Ship jerk, catch (k u/s³) | Catch time (s) | Exit error (°) |
|---|---|---|---|---|---|---|---|---|---|
| Shipped | 24.4 | 129.3 | 6 132 | 65 096 | 59 229 | 14 400 | 20.8 | 0.95 | 0.5 |
| **Analog triggers · squeeze** | 7.3 | 20.6 | 2 620 | 5 410 | 9 297 | 1 487 | 6.1 | 1.01 | 8.8 |
| Analog triggers · slam | 7.5 | 24.8 | 3 264 | 4 841 | 11 005 | 1 500 | 5.7 | **0.53** | 6.7 |
| Analog triggers · feather | 6.6 | **17.2** | **2 035** | **4 663** | 9 807 | 1 487 | **5.0** | 1.61 | 7.9 |
| Analog only (shipped hook, camera, let-go) · squeeze | 7.3 | 89.4 | 6 139 | 43 169 | 9 566 | 13 246 | 17.8 | 0.84 | 2.4 |
| Catch and swing (round 1) | 3.6 | 126.4 | 2 491 | 63 536 | 52 184 | 5 007 | 8.4 | 0.79 | 0.3 |
| All smooth (round 1) | 3.6 | 50.4 | 2 443 | 12 759 | 33 498 | 1 785 | 7.4 | 0.78 | 9.2 |
| *Hard pin* (control) | 50.6 | 125.2 | 6 132 | 63 166 | 62 384 | 14 400 | 20.8 | 0.70 | 0.4 |
| *No plant* (control) | 0.4 | 0.7 | 52 | 7 | 902 | 439 | 0.1 | — | — |

Worst single runs (Analog triggers): ball stop 7.5, hull catch 3 949 (slam), hull leave 10 990 (slam),
wind-in 11 085 (slam), exit error 19.2° (feather). Shipped worst: 63.3, 6 134, 141 099, 77 112.

**Noise band.** Seeds 7–12 reproduce the analog rows within about 3% (squeeze catch 2 683, leave 5 142,
wind-in 9 333). **Frame rate.** At 30 Hz the analog rows hold or improve (squeeze catch 2 034, leave 4 247,
wind-in 8 902), and every analog row still beats shipped on every jolt column. Ball stop and yank double,
as a per-frame step does at half the rate.

**Reading it.** Against shipped, the analog scheme cuts every jolt: the catch 2–3×, the leave 12–14×, the
wind-in 5–6×, the camera ~10×, the ball yank 5–8×. The quick stop itself (7 u/s per frame) is the
S-curve at its 450 u/s² peak; `anStopDecel` 300 gives round 1's ~5. The feathered pull is the smoothest
catch (2 035), and the slam the fastest (0.53 s, faster than shipped's 0.95). With every other dial
shipped ("Analog only"), the triggers alone fix the stop and the wind-in but not the leave or the camera,
so those round-1 pieces are still needed. The cost is the exit drift of the soft let-go (7–9° mean).

### Found

Most of the round went into **handoffs**: each place where one motion model hands the ship to another
was a one-frame jolt until it carried position, rate and force across. In the order found:

1. **A full pull with a rigid chain whipped the hull at ~2 100°/s.** The ball becomes an anchor and the
   stiff chain yanks back a ship flying straight away from it, reversing its velocity through near-zero.
   Fix: the full pull starts the hook round the still-stopping ball (round 1's Catch and swing).
2. **The S-curve hook restarted the turn from zero.** A ship already curving round a braking anchor
   (−125°/s) stopped turning for a frame. Fix: the hook adds a carried turn that starts at the ship's own
   incoming turn rate and fades out by the hook's end.
3. **The hook's stretch was snapped back.** The hook lets the chain stretch ~9 u so the ship can swing
   wide; a rigid chain then reeled that in within two frames (87 000°/s²). Fix: the reel-back starts at
   the shipped `hookReturnRate` 5/s and ramps to the chain's own rate over 0.5 s, from where the ship
   actually is.
4. **A slack chain's outward pull switched on in one frame** when the hook ended. Fix: its target eases
   in over 0.5 s.
5. **Planting with the ball dead astern picks the orbit's direction from noise.** The sideways velocity
   that `BeginPivot` reads is ~0, so its sign (or `AnyPerpendicular`'s world-axis fallback) decides which
   way, and in which plane, the ship orbits. The carried turn tipped the heading across the radial line
   and the orbit reversed mid-hook. Lab fix (analog only): when the ship is within 30° of straight away,
   orbit in the hull's turning plane, in the direction it is already turning, and never flip the
   direction once chosen. **This is a shipped behaviour** (planting after flying straight can pick an
   arbitrary spin plane in 3D) and needs checking in the editor.
6. **The planted hull lags its velocity by one frame (shipped).** `ThresherVesselTransformer.RotateShip`
   runs before the move, so while planted the hull faces the previous frame's velocity, and on the frame
   a plant begins it does not turn at all. Lab dial `hlPost` (on in the analog preset) turns the hull with
   the frame's own velocity change, as towing already does. Graduates as a transformer change.
7. **A slammed RT is the biggest jolt the scheme can make.** From slack to rigid in one frame, the chain
   hauls a let-out ball in and the hull whips at 382 000°/s² on the wind-in. A rigidity slew limit
   (`elSlew`) caps it: 1.5/s → 6 300, 3/s → 22 800 (slammed flights, letout-then-tightened, 6 seeds).
   The preset uses 2/s (0.5 s from slack to rigid).
8. **A stiff spring is a frame-rate bug.** A 30 rad/s chain at 30 fps sits at the edge of the
   integrator's stability: the first 30 Hz run read 65 000°/s² on the wind-in against 10 000 at 60 Hz.
   Fix: the chain is sub-stepped (ω × h ≤ 0.15). **A C# port must do the same** (or integrate the spring
   implicitly), because the game's frame time varies.

### Recommendation (a proposal, not a decision)

The **Analog triggers** preset delivers the scheme as asked and is the smoothest thing the lab has
scored. The next round should be **flown with a pad**: the scorecard's three pilots are scripts, and
how a real thumb holds a half pull (the sliding anchor) is the open feel question.

### Decision needed (designer)

1. **The scheme:** is LT-brake / RT-rigidity (flipped) the direction? It removes the winch: chain length
   is no longer chosen, it falls out of slack × speed. Measured with RT released (6 seeds): towing through
   30–90°/s turns the ball reaches only 31–33 u (the shipped winch lets it out to 140 u), and a planted
   orbit spins out to 72–79 u on average, 89–101 u at most. A towed ball can no longer be let out long
   on purpose; if that matters, `elSlack` lower or a slack rest length longer than 22 u would restore it.
2. **The sliding anchor:** how much a partial LT swings the ship (`anAnchor` 2; lower = the ball slows
   before the ship feels it).
3. **The final stop:** `anStopDecel` 450 (snappier) vs 300 (round 1's softness).
4. **The crack:** keep the 2/s rigidity slew, or let a slammed RT crack as hard as it likes?
5. **The let-go:** the soft let-go's 7–9° exit drift (carried from round 1).

### Checked (round 2)

- `parity.cjs`: the shipped path is untouched by the analog code. Same result as round 1: 1 224 frames,
  0.80 × tolerance, all three planted controls caught.
- `verify_lab.cjs`: PASS. Both screenshots read; the trigger gauges, the legend and the phone's touch
  buttons were moved apart after the first read showed them overlapping.
- Every analog jolt column was probed at its worst run, frame by frame (`window.__lab.runRows`, with a
  probe that prints ±6 frames round each column's peak), until no single-frame step was left in the
  catch, hook, handoff or leave. The eight items in Found are what those probes turned up.
- Re-scored on seeds 7–12 and at 30 Hz (above).

### Not modelled (added this round)

- The elastic chain is a spring model the game does not have (the shipped chain is a hard max-distance
  rope with a winch). Spin is conserved exactly (r × v) as the elastic radius changes; the shipped winch
  spends spin by √(old/new) when paying out.
- Keyboard and touch ramp a trigger over `inKeyRamp` 0.35 s; only a pad gives a real analog pull.

---

## Round 1 (2026-10-13), the lab and the first scorecard

*Round 1's numbers were taken on the round-1 windows (1 s catch window, wind-in 1.2 s after the press).
Round 2 re-measured every row on its own windows; read the two tables apart.*

### What is jarring today (measured on the shipped model)

Five separate jolts, each a value that changes in **one frame**:

| # | When | What happens | Size (60 Hz) |
|---|---|---|---|
| 1 | LT down: **the catch** | The skid brakes the ball at 7/s for 0.3 s, but the rope is taut and keeps dragging the ball along with the ship, so after the skid the ball is still doing **62.8 u/s** (90% of cruise; C# reference trace, scenario `reeled-turn-plant`). `BeginPivot` then sets it to 0. **The instant stop.** | 63 u/s in one frame, reeled in |
| 2 | Hook-on | The heading starts turning onto the orbit at a flat `hookTurnRate` 300°/s from the first frame; the hull follows through `pivotFacingRate` 25/s. | 6 100°/s² hull |
| 3 | The spectate cut | When the spin is fast enough the camera detaches to the launch vantage. The entry eases from the pose the camera was in at that moment (`_easeFromPos/_easeFromRot`), so a camera that was turning with the hull at up to 240°/s **stops turning in one frame**. | 14 400°/s² camera |
| 4 | RT up while planted: **the wind-in** | From full let-out the chain reels in at `reelIn` 331 u/s: 118 u in 0.36 s. Spin conservation carries the orbit to the ball-speed cap (479 u/s on a 22 u chain, about 1 100–1 250°/s). | 58 000–78 000°/s² hull |
| 5 | LT up: **the let-go** | The orbit's turn stops in one frame (the hull also snaps up to ~44° to the exit line, its lag behind the velocity at the fastest spins), and the ball goes from 0 to `yank` × ship speed in one frame, then the hard rope snaps it to the ship's speed. | 59 000°/s² hull (131 000 worst); ball 118 u/s (220 worst) |

### The approaches (every one is a set of lab dials; at their shipped values the shipped path runs)

| Approach | Dials | Idea |
|---|---|---|
| **Longer skid** (tuning only) | `skid` 12, `skidSeconds` 0.5 | Can the shipped dials fix the stop? |
| **Quick stop, linear** | `bkMode` 1, `bkCurve` 0, `bkSeconds` 0.3 | The ask in its plainest form: the ball decelerates to rest at a constant rate while the rope pays out, and becomes the pivot already still. The stretch it took is handed to the existing critically damped reel-back. |
| **Quick stop, S-curve** | `bkCurve` 2, `bkSeconds` 0.35 | The deceleration itself fades in and out. |
| **Catch and swing** | quick stop S-curve + `hkEarly` 1, `hkShape` 1, `hkSeconds` 0.5 | The ship starts curving onto the orbit at the press, round the still-braking ball, and eases in and out of the hook instead of snapping to 300°/s. |
| **Gentle wind-in** | `wdReel` 0.4 | The planted reel-in at 0.4 × `reelIn` (0.9 s from full let-out). Spin is conserved per unit length, so the speed it ends at is unchanged. |
| **Soft let-go** | `rlSpinDown` 0.3, `rlRopeGive` 0.3, `yank` 0 | The turn winds down (at most 0.3 s and `rlSpinMaxDeg` 30°) instead of stopping; an aimed launch leaves early by the angle it will still turn. The ball leaves the pivot at rest and a tension-limited rope draws it up to speed. |
| **Smooth camera** | `cmAngAccel` 1500, `cmCarry` 0.35 | The follow frame changes its turn rate at most 1 500°/s², and the spectate entry keeps the camera's turn and glides it to a stop over 0.35 s (the aim is where the glide ends). |
| **All smooth** | all of the above + `spEase` 0.4 | |
| *Hard pin* (control) | `skidSeconds` 0 | Planted defect: pinned at full speed. Must rank worst on the stop. |
| *No plant* (control) | LT never pressed | The floor ordinary flying produces. |

### Scorecard

`runBatch()`: 6 seeds × 3 flights (reeled in; let out then wound in while planted; let out and held),
60 Hz, the mean over runs of each run's peak. Lower is smoother except **Catch time** (the cost of
smoothing; lower is snappier) and **Exit error** (how far the ship leaves from its line).

| Approach | Ball stop (u/s/frame) | Ball yank (u/s/frame) | Hull jolt, catch (°/s²) | Hull jolt, leave (°/s²) | Hull jolt, wind-in (°/s²) | Camera jolt (°/s²) | Ship jerk, catch (k u/s³) | Catch time (s) | Exit error (°) |
|---|---|---|---|---|---|---|---|---|---|
| Shipped | 24.4 | 118.1 | 6 132 | 59 148 | 57 747 | 14 400 | 20.8 | 0.95 | 1.0 |
| Longer skid | 23.4 | 125.5 | 6 127 | 62 131 | 71 511 | 11 757 | 19.7 | 1.14 | 0.5 |
| Quick stop, linear | **2.8** | 118.0 | 6 155 | 59 111 | 72 572 | 14 400 | 24.3 | 0.99 | 1.1 |
| Quick stop, S-curve | 3.6 | 118.2 | 6 323 | 59 401 | 70 132 | 14 400 | 25.8 | 1.05 | 0.9 |
| Catch and swing | 3.6 | 114.8 | **2 491** | 57 577 | 66 791 | 5 007 | 8.4 | **0.79** | 0.4 |
| Gentle wind-in | 24.4 | 127.0 | 6 132 | 63 653 | **40 443** | 14 400 | 20.8 | 0.95 | 0.4 |
| Soft let-go | 24.4 | **40.6** | 6 132 | **10 338** | 57 747 | 14 400 | 20.8 | 0.95 | 6.0 |
| Smooth camera | 24.4 | 118.1 | 6 132 | 58 662 | 57 747 | **1 500** | 20.8 | 0.95 | 1.6 |
| **All smooth** | 3.6 | 54.0 | 2 443 | 10 293 | 43 231 | 1 889 | **7.4** | 0.78 | 7.7 |
| *Hard pin* (control) | 50.6 | 111.8 | 6 132 | 55 954 | 72 067 | 14 400 | 20.8 | 0.70 | 0.4 |
| *No plant* (control) | 0.4 | 0.7 | 52 | 36 | 131 | 212 | 0.1 | — | — |

Worst single runs (same batch): shipped ball stop 63.3, Quick stop S-curve 5.4; shipped leave jolt
130 583, Soft let-go 28 226; shipped ball yank 220.4, Soft let-go 115.7; Soft let-go exit error 12.6°,
All smooth 16.1°.

**Noise band.** Seeds 7–12 reproduce every column within about 5% except **Hull jolt, wind-in**, which
swings 58 000 ↔ 78 000 for the shipped model on the two seed sets (Gentle wind-in: 40 443 ↔ 38 167;
All smooth 43 231 ↔ 35 074). Only differences outside that range are claimed there.

**Frame rate.** At 30 Hz the ordering of every jolt column above holds except the camera's (exit error is
noisier there: Catch and swing reads 5.4° against shipped 2.3°). In the camera column there the spectate cut lands
on the frame before the chase camera has started turning, so jolt 3 does not show (shipped 622°/s²). At
60 Hz it lands mid-turn. The cut is a one-frame race the frame rate decides, which is itself a reason to
carry the camera's turn into the entry.

### Found

- **The skid cannot stop the ball, whatever its dials.** The rope, not the brake, sets the ball's speed
  during the skid. A harder, longer skid (Longer skid) still pins at 23 u/s mean, 56 worst, and costs
  0.2 s of catch time. A quick stop has to move the ball kinematically and let the rope pay out; tuning
  `skid`/`skidSeconds` cannot do it.
- **A quick stop fixes the ball and nothing else.** Both quick stops drop the ball stop 24 → 3 u/s
  (63 → 5 worst), but the hull jolt at the catch is unchanged (6 100°/s²): that comes from the hook-on.
  Only the eased hook from the press (Catch and swing) also takes the hull jolt down (6 100 → 2 500), and
  it catches *sooner* (0.95 → 0.79 s) because the ship turns while the ball brakes.
- **The camera freezes mid-turn at the spectate cut** (jolt 3, confirmed in
  `ThresherExecutor.UpdateCamera`: the entry eases from `_easeFromPos/_easeFromRot`, captured once).
  This was not on the list going in; it may be the "instant stop" a pilot feels most, since it is the
  whole view stopping.
- **The planted wind-in is the largest jolt the ability has** (58 000–78 000°/s²) and ends with the ship
  orbiting at the ball-speed cap, ~1 100°/s. Slowing the reel keeps the payoff and cuts the jolt by a
  third to a half (40 443 vs 57 747 on seeds 1–6, 38 167 vs 77 868 on seeds 7–12). The 1 100°/s spin is shipped behaviour (THRESHER.md §4, "conservation may carry you past the
  cap"), and it is why no let-go can be both smooth and on-line at the top end.
- **Soft let-go trades smoothness for line.** The leave jolt drops 59 000 → 10 000°/s² and the ball yank
  118 → 41 u/s, but the ship leaves 6° off its line on average (12.6° worst), because the hull lags its
  velocity by up to 44° at the fastest spins and a wind-down capped at 30° cannot both stop smoothly
  and land on the aim. `rlSpinMaxDeg` is the dial for that trade.

### Recommendation (a proposal, not a decision)

**All smooth** is the lab's lead: every jolt column drops, from 1.3× (the wind-in) to 8× (the camera),
the catch is faster (0.78 s), and the one cost is the exit error (7.7° mean). If the designer finds the exit drift
unacceptable, the next round should first try a higher `pivotFacingRate` (less hull lag to unwind)
before shrinking `rlSpinMaxDeg`.

### Decision needed (designer)

1. **The catch:** Catch and swing as is, or the quick stop alone (keeps the shipped hook)? Brake time
   (`bkSeconds` 0.35) and curve?
2. **The let-go:** is a ~6° exit drift acceptable for the soft let-go, or should the shipped snap stay
   for the aimed launch only?
3. **The wind-in:** slow the planted reel (0.4 × `reelIn`), or is the violent wind-in the point?
4. **The camera:** glide into spectate (0.35 s) and the 1 500°/s² follow limit?

Fly them in the lab, then **Decisions ▸ Record decision**. The next round starts by reading that log.

### Checked

- `node Tools/Build/thresher_plant_lab/parity.cjs`: the page's solver against the **shipped**
  `ThresherChainSolver.cs` (compiled from its real path by `trace.sh` with the chain harness's stubs) on
  4 scripted cycles (turn-and-plant reeled in; let out, plant, wind in; the degenerate straight plant;
  an armed launch), **1 224 frames, mode equal on every frame, positions within 0.80 × tolerance**
  (0.02 u + 2·10⁻⁴ × distance from origin; the C# side is float32 and its rounding grows smoothly with
  distance, never in a step). Negative controls, each a change to the page only: skid 7 → 7.5 misses by
  46×, yank 0.5 → 0.55 by 757×, hookTurnRate 300 → 290 changes the mode sequence. All three are caught.
- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/ThresherPlantLab.html`: PASS (no console
  errors at 1600×900 or on a phone, layout fits, hooks present, SPEC within SHIPPED ranges, manual clock
  advances, `runBatch` deterministic, stage not blank). Both screenshots read.
- The yardstick was wrong three times before it was trusted: the catch window first overlapped the
  wind-in (No plant read 14 u/s ball, 1 600°/s hull); the first rope-give spring could pull but not
  brake, so the ball overshot the ship and the hard rope snapped it 596 u/s; and the lab's own camera
  limiter chattered at its cap. Each was found by probing the worst run of a column frame by frame, and
  fixed before the table above was taken. The No-plant control now reads ~0 on every column, and Hard pin
  ranks worst on the stop.
- Hard pin ranks worst on Ball stop (50.6), the control's job.
- Decision log: published with `db` + `user`; `ArtifactData list decisions` reads the empty log.

### What this lab does NOT model

- Flight is in one plane: yaw only. Pitch, roll, planted steering (`pivotSteer`) and tilting the orbit
  with the aim are not modelled.
- The camera is a simplified `CustomCameraController`: the follow-frame cap and the spectate ease as the
  executor does them, a zoom approximated as max(55, 1.15 × chain length), no sight window, no FOV.
- No prisms, smashes, hit-stop, shake, haptics, audio, chain links or element upgrades. Perfect Release's
  boost is off.
- The hull, grip, nose speed and camera are re-implemented from `ThresherVesselTransformer`,
  `VesselTransformer` and `ThresherExecutor`. Only the chain solver is held to the C# by the parity gate.
- The scorecard runs at a fixed 1/60 s (and was checked at 1/30 s); the game's frame time varies.
- The approaches are lab code. None is in the game.

### Graduation (when the designer has decided)

Each approach maps onto a default-off dial on `ThresherDials` (`ThresherConfig.asset`), so the shipped
behaviour is the default and the tests stay green:

- quick stop → a brake-to-rest branch in `ThresherChainSolver.Step`'s skid (`brakeSeconds`, `brakeCurve`),
  with the stretch handed to the existing hook slack;
- eased hook → an S-curve branch beside `TurnTowards` in `StepPivot` (`hookSeconds`), and starting the
  pivot at the press (`hookFromPress`);
- gentle wind-in → a planted reel-rate multiplier;
- soft let-go → a spin wind-down in `ThresherVesselTransformer.RotateShip`, a launch lead in `TryLaunch`,
  and a tension-limited rope window after `Release`;
- camera → a turn-carrying entry in `ThresherExecutor.BeginSpectate`/`UpdateCamera`, and an
  angular-acceleration limit beside `CustomCameraController.MaxFollowTurnRate`.

Each lands with an edit-mode test named for the behaviour (e.g. `Plant_QuickStop_BallIsAtRestWhenItBecomesThePivot`,
`Release_WindDown_LeavesWithinTheCapOfTheAim`). The pick is then run back through `parity.cjs` (a new
scenario per branch), so the lab and the game agree on the chosen path. Anything not verified in the
editor goes to `Docs/QA/QA_BACKLOG.md`.
