# Squirrel AI — skimming Skim Race like a person (first vertical slice)

**Status: DESIGN (2026-09-28), not implemented.** The first vessel built on
[`ARCHITECTURE.md`](ARCHITECTURE.md): a Squirrel that launches off the ribbon, holds a skim line,
drifts its corners, takes its crystals from the right side of the ribbon, rides wakes, and uses its
Boost Ring, at five difficulty tiers.

Every number here comes from the shipped assets through
**`python3 Tools/Build/squirrel_skim_model.py`** (a reader that writes nothing; about 12 s). Its
section numbers are quoted as "model §N". Re-run it before trusting a number, because the numbers
move when the assets do. Defect numbers `G#` refer to [`CURRENT_STATE.md`](CURRENT_STATE.md).

---

## 1. The Squirrel, as an AI has to fly it

| | |
|---|---|
| **Throttle** | The two-stick scissor `XDiff`, from 0 to 1. A human's resting value is 0.5. Target speed = `XDiff × 60 × BoostMultiplier`, so **cruise is 60 u/s and top speed is 300 u/s** at the 5× boost cap. |
| **Speed source** | **Skim energy only.** Every prism that ENTERS the skimmer sphere adds `+0.1 × the Time multiplier (×1 → ×2)` to `BoostMultiplier`. Boost decays at **0.3/s** toward 1 (`SkimmerBoostPrismEffect`, `VesselTransformer.DecayBoost`). |
| **Skimmer** | A sphere of radius **7.5 u** at Space 0, growing to **15 u** at Space 10 (`Skimmer.prefab` radius 0.5 × the Squirrel's `Scale` 15 → 30). It **collects no crystal** (`skimmerCrystalEffectsSO` is empty). It pulls a crystal toward itself at only 4 u/s. |
| **No line-holding assist** | The prefab still overrides three skimmer STAY effects: align-to-prism, boost-by-prism, and distance haptic. `SkimmerImpactor`'s stay path is commented out, so **none of them runs**. Energy comes from prism entries alone, and nothing helps a pilot hold a line. |
| **The penalty** | If the HULL touches any prism, `VesselResetBoostPrismEffect` runs. It **resets boost straight to 1×** and raises the `onSkimmerShipCollision` stat. The same contact also applies a slow (`SquirrelVesselChangeSpeedByPrism`). A danger prism adds an elemental hit on top. Super-shielded mass cannot be damaged, so the track survives the contact. The pilot's speed does not. |
| **Turning** | 120°/s pitch and yaw, **independent of speed** (`RotationThrottleScaler 0`), and ×1.8 while drifting. Minimum turn radius is 29 u at cruise and **143 u at top speed**, or 80 u drifting at top speed. |
| **Steering lag** | The transform slerps toward the commanded rotation at `LERP_AMOUNT` 1.5/s. That is **τ ≈ 0.67 s, which is 200 u of travel at top speed** before a heading change fully lands. |
| **Flight model** | The vector model (`vectorFlightModel: 1`, `SQUIRREL_DRIFT.md`): thrust acts along the nose, a drift can overshoot to 1.25× (`driftOvershootCeiling`), and throttle stays live through a drift (`driftThrottlePolicy: Live`). Drift grip is 0.25. |
| **Drift** | On LT, analog for humans. It is bound ONLY in device overrides: touch `OnlyLeftStickAction (12)` and gamepad `LeftStickAction (2)`. The shared map is empty. An AI reports `Touch`, so its drift resolves to `12` and is binary (G4, G9). |
| **Boost Ring** (Mass, RT) | 8 danger prisms (4 u cubes) on an 8 u radius, laid **100 u ahead of the NOSE at any speed** (`leadSeconds` −1.5 clamps to 0). The hull must stay within **6 u of the ring's axis**. The cooldown is 20 s at rest and 10 s at Mass 10 (`SquirrelTubeActionExecutor.TubeReady`). Mass 5 ("Twin Rings") adds a second ring. Bound to touch `OnlyRightStickAction (11)` and gamepad `RightStickAction (1)`. The ring is **live for its own pilot from the frame it lands** — see the note below this table. |
| **Passives** | Skimming (Time). Steal (Space): every non-super-shielded prism the skimmer touches changes hands. Crystal Joust (Charge): skimming PAST a slower rival steals petals (`VesselOvertakeBySkimmerEffectSO`). |
| **Its own wake** | **Two rails**, each 0.83 × 0.83 × 6.1 u, one prism every **7 u**. They sit **±9.7 u either side of the line flown**, with inner edges 9.25 u from it. This comes from `Gap 18.5` cut from a 20 u slab (`VesselPrismController.CreateBlock`), with the volume multiplier fixed at 1.35. The hull flies the clear corridor between the rails. Other pilots can skim them at once; the pilot itself can skim them 1 s later (`SelfTrailContactConfig`). Drifting lays them shielded. |

**Note on the Boost Ring's ownership.** `BoostRingBuilder` stamps `ownerID = "<name>::Tube::…"`, which never equals the pilot's name. So the self-trail grace does not apply to the ring. That matters because at top speed the ring is reached in 0.33 s, inside what would otherwise be a 1 s grace.

## 2. Skim Race, as the AI meets it

**The card.** `ArcadeGameSkimRace.asset` is **Squirrel only, 1–12 players**, with intensities 1–4.

**The objective.**
- Collect `waypoints × laps` crystals of your own domain.
- Each player's crystal respawns at the NEXT authored anchor, **on a random 35 u sphere around it** (`CrystalManager.GetSpawnPointAroundAnchor` places it on the shell of that sphere, not inside it).
- A crystal is mandatory; it waits until you collect it.
- Collecting takes the **hull touching the crystal's trigger: a 24 u sphere** (1.2 × the prefab's root scale 20, `BigCrystalVariant`).

**The ribbon.**
- `SpawnableWaypointTrack` lays one ordered `Trail` of **10 × 1 × 3 plates every 12 u**. Each plate is laid flat with `LookRotation(tangent, world up)`.
- The scene's `SegmentSpawner` **super-shields** the plates. So the contact envelope a hull must avoid and a skimmer must touch is the stellated octahedron.
- That envelope reaches **±15 u across, ±1.5 u thick and ±4.5 u along** per plate. Through the plate's centre its cross-section is exactly the diamond `|x|/15 + |y|/1.5 ≤ 1`.
- Waypoint marker plates are 2× that size.
- The ribbon is environment-owned, so it is nobody's "own trail".

**The four tracks** (model §8, §9). "Apex miss" is how far a vessel on its minimum circle is thrown off the ribbon at the sharpest corner. "Crystal beyond the skim zone" is how far a pilot already on the crystal's side of the ribbon must still leave skim reach to take it (§5.6).

| Intensity | Shape | Length | Ribbon prisms | Sharpest turn | Apex miss at top speed (drifting) | Crystal beyond the skim zone, p95 / max |
|---|---|---|---|---|---|---|
| 1 | flat octagon, 45° corners | 4,308 u | 359 | 46° | 12.6 u (7.0 u) | 3.5 / 3.5 u |
| 2 | tilted loop, spline | 5,569 u | 464 | gentle | ~0 | **29.9 / 38.9 u**. Its anchors sit 28–35 u off the spline (G13). |
| 3 | dumbbell, spline | 9,846 u | 821 | gentle | ~0 | 3.5 / 5.1 u |
| 4 | 3D loop, polyline | 8,205 u | 684 | **157°** hairpin at waypoint 0, then 70° and 62° | 566 u (314 u) | 3.6 / 4.3 u |

## 3. The central finding: the skim economy is a FLIP-FLOP

**Duty** is the fraction of distance flown with a prism field inside skim reach.
- Energy gain is `0.1 × speed × duty / spacing` per second.
- Decay is a flat 0.3/s.
- Speed is itself `60 × boost`, so gain grows with speed and decay does not.

The economy therefore has **two stable states and an unstable threshold between them** (model §1, §2, §7).

All figures below are at full throttle (XDiff 1).

| Situation | Result |
|---|---|
| **Take-off** from 1× on the ribbon | Needs duty **≥ 0.60**. Below that the vessel stays pinned near 60 u/s: 0.50 duty for 30 s ends at 1.10×. |
| Take-off at 1.00 / 0.80 / 0.70 / 0.60 duty | Reaches 5× in 5.9 / 8.3 / 10.8 / 18.2 s |
| **Holding** 5× | Needs duty **≥ 0.12**. Once flying, one prism in eight is enough. |
| One **hull touch** | Back to 1×. Recovery takes **5.9 s of perfect skimming**. |
| At Time 10 (×2 energy) | Take-off threshold halves to 0.30 |
| One **Boost Ring** pass | 1× → 1.8×. Take-off now needs **0.33**. |
| Riding **one wake rail** (7 u spacing) | Take-off needs **0.35** |
| Riding **both rails** of a wake (Space ≥ 2.5) | Take-off needs **0.18** |

Five consequences shape everything in §5:

1. **The launch is where precision matters.** Take-off needs a sustained majority of the ribbon. Cruising needs almost none of it.
2. **Once the boost is saturated, the pilot has surplus to spend.** It can go on corners, shortcuts and rivals.
3. **A hull touch is the single most expensive mistake** in the mode. It deletes six seconds.
4. **Throttle must be full.** Take-off duty scales as `0.6 / XDiff`. A human resting the scissor at 0.5 cannot take off at all, and neither can a bot that does not push.
5. **Anything denser than the ribbon is a launch pad.** That includes a Boost Ring and any wake, your own or a rival's.

## 4. Why today's AI cannot skim — four independent causes

1. **Its throttle cannot take off at intensities 1–2** (G3).
   - `XDiff = lerp(0.2, 0.9, intensity/4)`, which gives 0.375 / 0.55 / 0.725 / 0.9 for intensities 1–4.
   - Those need 160% / 109% / 83% / 67% duty. The first two are impossible even with perfect skimming.
2. **It flies at the crystal, not the ribbon** (model §10).
   - A straight line from one crystal to the next skims **11% / 2% / 11% / 17%** of its length on intensities 1–4.
   - Even the best 10% of those lines reach only 0.34 / 0.07 / 0.36 / 0.47, never the 0.60 take-off needs.
   - **13% / 6% / 17% / 22%** of the lines pass through the ribbon's envelope, which is a hull touch and a reset.
3. **Its steering cannot hold a shell a few units thick** (G1).
   - Below 3° of error it barely corrects. A 3° error drifts 16 u/s sideways at top speed.
   - It also ignores the 0.67 s rotation lag.
4. **It is blind to prisms and has no kit** (G4, G10).
   - It has no hull-contact prediction.
   - It has `drift: 0`, and `drift: 1` would press a control the Squirrel does not bind.
   - It has no Boost Ring, and no idea that wakes exist.

Any one of these alone keeps it on the ground. The design fixes all four.

## 5. The design

### 5.1 Everything is a prism

The track, both halves of every wake, a Boost Ring, a crystal's shielded ring, a rival's rails — all of them are prisms. Skim energy does not care which one it came from.

- The planner reasons per prism, through that prism's contact envelope. The envelope is a plain box, a shielded octahedron, or a super-shielded stella (`ShieldShellMath`, R6).
- That makes the Skim Race ribbon one case of a general rule rather than a special one.
- A ribbon's ORDER (its structure) is what lets the planner look ahead. The envelope is what keeps it honest.

### 5.2 The skim zone is a shell

The skimmer touches a prism when the vessel is within `r_s` of that prism's envelope. The hull must stay clear of the envelope. So the vessel skims safely anywhere its distance `d` to the envelope satisfies:

```
r_h · safety  <  d  <  r_s − precisionMargin
```

- That is a **shell about 5 u thick at Space 0**, the same thickness all the way around the ribbon.
- `r_h` is **measured at runtime** from the hull colliders' world bounds and never typed in.
- **Over or under the plate** the envelope is flattest: at most ±1.5 u, peaking on the plate's diagonals. The outer edge there is **9.0 u** from the mid-plane at Space 0, and 16.5 u at Space 10. This is the default place to sit.
- **Beside the plate**, in its plane, the envelope is a chain of diamond tips at ±15 u, so the zone runs out to 22.5 u. It works equally well; it is just less even.
- **Where around the ribbon to sit is a free choice.** Spend it on the next crystal (§5.6) and on the next corner's inside.
- Waypoint markers are twice as thick. The planner re-solves the shell over the look-ahead window and flies the INTERSECTION, so a marker lifts the line rather than clipping the hull.
- **Precision margin:** a precise pilot uses more of the shell; a sloppy one stays in its middle.

### 5.3 Following the ribbon

- **Find it.** In Skim Race, `SkimRaceController` hands the course over explicitly: it publishes the track's `Trail` through the objective source's `TryGetRoute` (ARCHITECTURE §2.2). Elsewhere, use the nearest long environment-owned or wake `Trail` in the prism field.
- **Hold a cursor; don't search.** A `RibbonCursor` (index + lerp + direction) advances each frame by the distance flown.
  - Each step is O(1) and bridges holes exactly as `Trail`'s own walks do.
  - Direction = toward the next own crystal along the ribbon.
- **Aim.** The reference point is the ribbon at `s + L`, offset into the chosen side of the shell.
  - Choose `L ≥ speed × (τ + reaction) + margin`. That pre-compensates the 0.67 s lag.
  - Feed the ribbon's curvature ahead to the controller as feed-forward.
  - The PD term closes the remaining error (ARCHITECTURE §2.5).

### 5.4 Three phases

| Phase | When | Priority |
|---|---|---|
| **LAUNCH** | Boost below ~4.5× | Duty above everything. Fire the Boost Ring (§5.9). Ride any wake in reach (§5.8). Stay in the shell through corners, slowing if needed. |
| **CRUISE** | Boost saturated | Speed. Keep duty comfortably above the 0.12 hold threshold over a rolling window (target ≥ 0.25). Spend the surplus on cut corners, shortcuts and rivals. |
| **RECOVER** | Just reset, or lost the ribbon | Get back into the shell on the shortest safe path, then LAUNCH. |

### 5.5 Corners

- **Plan speed.** A corner of curvature κ is flyable at `v ≤ ω_max/κ`. At 300 u/s the minimum radius is 143 u.
  - Polyline corners (intensities 1 and 4) have no radius at all. Some shell loss at each vertex is therefore unavoidable at speed.
  - In CRUISE that loss is cheap. In LAUNCH the vessel is slow and its circle small (29 u at cruise), so it can follow the corners.
- **Drift** when the heading change needed over the next half second exceeds what 120°/s delivers.
  - The ×1.8 turn rate cuts intensity 1's 12.6 u apex miss to 7.0 u.
  - Press it through the drift CAPABILITY (`TryGetBoundAction<DriftActionSO>`, which resolves to `12` on the Squirrel), never through a fixed control (G4).
  - Model the slide: under drift grip, the course lags the nose.
- **Sit on the inside.** Being free to choose where around the ribbon to fly (§5.2) means taking the shell's inside edge through a bend.
- **Intensity 4's 157° hairpin** cannot be skimmed at any useful speed. Brake, drift, and take the shortest line to the far leg.

### 5.6 Crystals: mostly a question of WHERE, not WHETHER

**On intensities 1, 3 and 4, a crystal never pulls the pilot more than ~5 u out of the skim shell** (p95 3.5 u, model §9). The reasons:
- Its 24 u trigger reaches most of the way to the ribbon.
- The shell lets the pilot sit on the crystal's side.

So on those tracks, taking a crystal means **rotating the line around the ribbon toward the crystal's side** as it approaches. That is a shift of up to ~20 u, which needs about 105 u (0.35 s) of lead at top speed. It does not mean leaving the ribbon.

**Intensity 2 is the exception** (G13). Its anchors sit 28–35 u off the spline, so a crystal sits up to 39 u beyond the shell (p95 30 u).
- There the excursion is real: an S-curve starting ~127 u (0.42 s) early at top speed.
- Price it against the flip-flop. In CRUISE a one-second excursion costs 0.3× of boost, so just take it. In LAUNCH, time the take-off so the first long excursion lands after saturation.

**Pick crystals by TIME along the course, not by distance in space.**
- Intensity 3's dumbbell runs two lanes 120 u apart in opposite directions. The crystal nearest in space can be half a lap away along the ribbon.
- `AIObjectiveScoring`'s commitment hysteresis stays. Its score becomes estimated arrival time.

**Shortcuts (Hard and above).** When saturated, compare two options:
- the ribbon route at about 300 u/s, and
- the straight line at a decaying boost, plus the cost of relaunching.

Take the faster. Shortcuts are legal, and they are what a strong human does.

### 5.7 The hull veto

This runs on every perception tick, at every tier:

1. Sample the planned path over the next ~0.5 s as a chain of capsules, each the hull's radius × the tier's safety margin.
2. Test the chain against the envelopes of the prisms in the forward cone. Use `PrismSpatialIndex.QueryCone`, then `ShieldShellMath.Capsule…` — the same math the contact tier resolves with.
3. On any overlap, move the offset outward or to another side of the ribbon.

A veto always beats a plan. Nothing in the mode is worth a reset.

### 5.8 Wakes and drafting

A Squirrel's wake is **two rails, a prism every 7 u each**. That makes a single rail 1.7× as rich as the ribbon (a prism every 12 u).

**Riding the wake's centre line.**
- From **Space 2.5**, the skimmer reaches **both rails** from the centre line. That is **3.4× the ribbon's hit rate**, and take-off needs only 0.18 duty (model §7).
- At Space 0, a shift of 1.75 u or more toward one rail reaches it. That still leaves the hull about 7.5 u from that rail, minus its own half-width.

**Drafting is the strongest move in the mode, and it EMERGES.**
- Every Squirrel flies the ribbon's shell, so its rails sit around the line a follower would fly anyway.
- A pilot on a rival's line skims the ribbon *and* both of that rival's rails: up to **4.4× the leader's hit rate**.
- The leader has only the ribbon; everyone behind has more.
- That is a natural rubber band nobody authored.
- The rails are live for everyone except their layer at once.
- Riding them also steals them, because Steal takes every non-super-shielded prism the skimmer touches.

**Laps 2 and later.**
- The pilot's own lap-1 wake is a hull-safe corridor on its own racing line, and it is live 1 s after being laid.
- A bot that re-flies its lap-1 line gets both rails for free.
- The shell solver sees the rails, so it can never fly *through* them.
- Drifting lays shielded rails. Their octahedra reach 8.42 u from the line, which is still clear of a centred hull.

### 5.9 Abilities

**Boost Ring.**
- **Value.** One pass is worth +0.8 at rest. That cuts the take-off threshold from 0.60 to 0.33. At Time ≥ 5 ("Live Wire", danger ×10) one pass is **+12, instant saturation**.
- **When to fire.** Its best use is the **start**, where it is ready at spawn, and **right after a reset**.
- **The press rule.** Press when all of these hold:
  - `TubeReady`;
  - boost is low;
  - the next ~100 u is straight;
  - the nose is on the course (the ring is laid along the NOSE, and the hull has only 6 u of axis clearance).
- **Replicated press.** It lays conserved mass that other peers must see (R5).
- **At Hard and above.** Fire it when no rival is close behind on your line. Skimming does not consume the ring, so a follower gets the same +0.8.

**Drift** (local press, motion only). See §5.5.

**Crystal Joust** (passive).
- At Hard and above: when faster, bias the line to pass a slower rival within skimmer reach, if that costs under 0.1 s of duty.
- At Expert: when slower, move the line off a faster rival's approach.

**Steal** (passive). Nothing to press. Riding a rival's wake already does it (§5.8).

### 5.10 Difficulty

**Every tier must TAKE OFF.** A bot crawling at 60 u/s reads as broken, not as easy.
- Tiers differ in how efficiently they cruise, how cleanly they take crystals, and how often they touch.
- Wake riding is the biggest lever after the launch, so which tier rides which wake is itself a difficulty knob.
- The values below are starting points. The harness calibrates them (§6 Phase 4).

**Behaviour per tier:**

| Squirrel knob | Rookie | Easy | Normal | Hard | Expert |
|---|---|---|---|---|---|
| Launch duty target | 0.70 | 0.75 | 0.85 | 0.90 | 0.95 |
| Shell use (fraction, centred) | 0.40 | 0.50 | 0.65 | 0.80 | 0.90 |
| Corners | brake | brake | drift | drift + inside line | drift + inside line |
| Crystal side-selection lead (× ideal) | 0.5 | 0.7 | 0.9 | 1.0 | 1.0 |
| Boost Ring | start only | start + recover | start + recover | + tactical | + tactical |
| Wakes | track only | own corridor | + one-rail drafting | + drafting | + drafting |
| Shortcuts | no | no | no | yes | yes |
| Joust | passive | passive | opportunistic | opportunistic | + defensive |

**Target metrics per tier (harness, per 3 laps):**

| Target metric | Rookie | Easy | Normal | Hard | Expert |
|---|---|---|---|---|---|
| Time to 5× from the start | ≤ 20 s | ≤ 15 s | ≤ 11 s | ≤ 9 s | ≤ 8 s |
| Hull touches | ≤ 6 | ≤ 3 | ≤ 1.5 | ≤ 0.5 | 0 |
| Lap time vs Expert | +60% | +40% | +20% | +8% | — |

## 6. Implementation plan

Each phase ships on its own, behind its own measurements.

### Phase 0 — unblock (small; no behaviour change on any other hull)

- Resolve the commit/drift control by capability in `AIPilot`, falling back to `LeftStickAction` so the Dolphin is byte-for-byte unchanged. This fixes G4.
- Add a read-only `VesselTransformer.CommandedRotation`. The controller steers the integrator, not the lagged transform.
- Add a non-allocating sibling of `Trail.LookAhead`, plus a point-at-(index, lerp) helper.
- Add `CSLogChannel.AI` with its label. The Logging toolbox reflects the enum.

**Quick win available today:** author the Squirrel's `defaultThrottleLow/High` at 1.0/1.0.
- Intensities 1–2 go from a 22.5 / 33 u/s crawl to a 60 u/s cruise.
- Every intensity's take-off threshold drops to its floor (0.60).
- This does not make the bot skim: its line still misses the ribbon (model §10). It removes the one cause that makes skimming impossible.
- A Boost Ring at the start cannot be had without code. The ability needs a replicated press, and the old timer cycler presses locally on the server only.

### Phase 1 — the flight controller (fleet-wide, parity first)

- `AIFlightController` (pure) plus a `FlightModel` read from the live transformer.
- Route today's targets through it.
- Edit-mode tests.
- Harness v0 re-measures `AI_ORBIT_BREAK.md`'s pursuit numbers. The 400/400 result must survive.

### Phase 2 — the skim line

- The `AIVesselDriver` seam and `SquirrelDriver`. The seam lands with `GenericDriver` (today's behaviour) and `DolphinDriver` (today's commit loop, moved with parity) — ROADMAP §1.
- `RibbonCursor`, the shell solver, and the LAUNCH / CRUISE / RECOVER phases.
- The hull veto.
- The Skim Race route hint, plus the Boost Ring at the start (a replicated press).
- Harness v1 runs all four tracks. It reports, per intensity, time to take-off, duty, touches and lap time, with model §10 as the "before" baseline.

### Phase 3 — race craft

- Corners: speed plan plus drift.
- Crystal side-selection, time-based crystal choice, intensity 2 S-curves, and shortcuts.
- The full Boost Ring policy.
- Wake and drafting lines.

### Phase 4 — difficulty

- `AIDifficultyProfileSO` tiers plus the Squirrel section.
- Calibrate against §5.10 in the harness.
- Needs decision D1 (ARCHITECTURE §4).

### Phase 5 — beyond Skim Race

- Joust lines.
- Then the same driver in Joust, Scurry, Regatta rails and Broadside.

## 7. Acceptance (measured, not eyeballed)

**Baseline, today (model §10).** Straight-line skim duty is 0.02–0.17. Between 6% and 22% of legs pass through the ribbon. No take-off happens at intensities 1–2.

**Harness, Expert, 200 seeded runs per intensity:**
- 5× within 8 s of the start on intensities 1–3.
- Launch duty ≥ 0.85; cruise duty ≥ 0.25.
- **Zero hull touches.**
- Every crystal collected in order.

**Tiers:** the ladder is monotone in lap time, and every tier takes off on every track.

**Regression:** `AI_ORBIT_BREAK.md`'s pursuit numbers are unchanged by Phase 1.

**Budget:** ≤ 0.1 ms per bot per frame and no per-frame allocation, checked with the `AI.*` profiler markers.

**MPPM:** a bot's Boost Ring is visible on a client, and it slams a client's Squirrel that flies into it.

## 8. In-editor verification (once built)

1. **Launch.** Skim Race, intensity 1, solo plus 3 bots at Normal. Watch the start: every bot fires its ring, locks into the ribbon's shell, and reaches top speed within ~11 s. A bot still at 60 u/s after 20 s has failed its launch.
2. **The line.** Turn on `CSLogChannel.AI` and the overlay. The line should hug the ribbon, lift at waypoint markers, and swing around the ribbon toward each upcoming crystal. The duty readout should sit high in LAUNCH.
3. **Crystals on intensities 1, 3 and 4.** They should be taken without the bot visibly leaving the ribbon.
4. **Intensity 2.** Crystals 30–40 u out mean long, early S-curves. Lap times will show it.
5. **Lap 2.** The bot rides its own corridor and never flies through its own rails.
6. **Drafting.** Two bots on one line: the follower's boost should climb visibly faster than the leader's.
7. **Intensity 4.** The hairpin at the start line should read as brake plus drift, not a crash.
8. **Live Wire.** Give a bot Time 5 (Toy Box ▸ Element Charger, in freestyle) and watch its Boost Ring. One pass should take it straight to 5×.
9. **Clean run.** Expert for three laps with no hull contact: the `onSkimmerShipCollision` stat stays at 0.

## 9. Open questions

1. **Intensity 2's anchors** sit 28–35 u off the spline, so its crystals are real excursions (G13). Is that intended?
2. **Intensity 4's closing hairpin** is 157° at the start line. Is that intended?
3. **The Boost Ring's exemption from the self-trail grace** is an accident of its `ownerID` format. It is load-bearing: the ring pays nothing above ~100 u/s without it. Should it be kept as it is, or made explicit in `SelfTrailContactConfigSO`?
4. **Drafting** is a strong, emergent rubber band (§5.8). Is it wanted as-is, or should Skim Race's comeback system account for it?
5. **Analog drift for bots** (G9): is it worth a seam, or is a binary drift fine for every tier?
6. **Difficulty source:** ARCHITECTURE D1.
