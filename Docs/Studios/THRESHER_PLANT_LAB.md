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
picked in the Approach tab. The Traces tab plots ball speed, hull turn rate and camera turn rate for both
over the last 7 s. The Scorecard runs every approach headless. Keys: A/D steer, Q (or Left Shift) holds
LT, E (or Space) holds RT, R restarts, Tab swaps A/B in Single view. Autopilot (on at load) loops seeded
plant cycles; any input takes over. Slow-mo plays at quarter speed.

---

## Round 1 (2026-10-13), the lab and the first scorecard

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
