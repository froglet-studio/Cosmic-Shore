# GIBBON — zero-gravity brachiation on two arms

**Code name:** Spider (`Spider.prefab`, `SwingingVesselTransformer`, `SpiderVesselHUD*`,
`SpiderCameraSettingsSO`, `SO_Class_Spider`). **Player-facing name:** Gibbon
(`VesselClassType.Gibbon = 13`). **Design record for the 2026-09-09 rebuild** — the second pass on
this vessel, after the first shipped build was judged "flawed and clunky … not the joyous movement
that Spider-Man games deliver". Everything below was designed on paper, put in front of a
three-judge panel (feel/mastery, physics/stability, codebase integration — 20 blocking findings,
all folded in), and then PROVEN offline by driving the shipped C# through Unity-shaped stubs with
real vector/quaternion math (`scratchpad` harness, 47 assertions — see §7). It has **not** been
opened in the Unity editor: §8 is the in-editor verification list.

## 0. Why the first build was clunky (and what each defect became)

| Shipped defect | What it did | What replaced it |
|---|---|---|
| Two screen-space cursors ray-cast from the camera | you could not read where a line would land in 3D | a WORLD reticle per arm, snapped onto the locked prism, aimed in a cone around the COURSE (§2) |
| Pump = "spread the sticks to shorten the circle" (`XDiff`) | an abstract lever with no physical read; the single-anchor state had no accelerator at all | the WINCH: reel a taut line and the angular-momentum law raises your speed (§3) |
| `SwingActionSO` bound to `FullSpeedStraightAction`, a DERIVED event | its RELEASE fired the moment any stick moved >0.3 — both lines dropped exactly when the pilot steered | `GibbonArmActionSO` bound on `LeftStickAction` (LT) / `RightStickAction` (RT): press = cast, release = fling |
| The hull never turned toward its velocity | the ship (and the hard-welded Fixed camera) faced away from travel mid-swing | `RotateShip` owned: nose on velocity, banked into the arc; DynamicCamera with lag (§5) |
| Quadratic drag while anchored | fought the only energy source; the terminal state was "slow" | drag is small and applies to coasting; the cap is a soft efficiency on the winch, never a brake |
| Integrate-then-project constraint | a per-frame "snap" that bled speed at a frame-rate-dependent rate and fired the kick every frame | ONE snap at the exact ray–sphere instant, then exact motion on the sphere (§3) |
| Sticks aimed cursors AND drove base pitch/yaw/roll | double duty | base rotation never runs; air control only from the component both sticks agree on |
| Replicas ran the sim; `velocityShift`/`throttleMultiplier` dropped in two branches | remote hulls fought their own replication; knockback and the danger slow silently vanished | `IsNetworkClient` early-return; one integrate line in every state |

## 1. Pillars

1. **One stroke = one beat.** CAST → SNAP → REEL → FLING. Every grab is a readable, timed event.
2. **Two arms, two triggers, alternate them.** LT = left arm, RT = right arm; each stick aims its
   own arm. Alternating swings that CARRIED pays a tempo bonus; both at once is the slingshot.
3. **No throttle, no gravity, no free energy.** Every u/s comes from the winch, soft-capped by an
   efficiency that fades to zero at `pumpSpeedCap`; drag brings anything above it back. Mass is
   conserved: the arms never create or destroy a prism except through the sweep (an active force).
4. **Mastery is timing and geometry.** Which prism (angle off course decides turn vs pump), when to
   let go (the velocity vector at release IS the launch), how deep to reel, and rhythm.
5. **The hull follows the velocity; the camera follows the hull.**

## 2. Controls

| Input | Free arm | Latched arm |
|---|---|---|
| Stick (that side) | aims the arm's reticle in a cone (`aimHalfConeDeg` 55) around the COURSE, resting at a yaw DERIVED from speed: `acos(|v| / castSpeed)`, floored at `restAimYawFloorDeg` 25 — 61° at 250 u/s, 73° at 150, 90° at rest — so the prism is ABEAM when the spool LANDS (a blind tap snaps in 0.2–0.4 s with no whiplash instead of coasting past an anchor ahead). X pushes out/in, Y up/down. | the WINCH on every device: **up = reel in, down = pay out (the brake), neutral = hold**. Stick X rolls the swing plane about the line (`swingSteerDegPerSec` 90). |
| Trigger press (that side) | CAST at the reticle: the locked prism, or a WHIFF to max range (visible retract at cast speed, tempo reset). | — |
| Trigger analog depth | — | reel = `smoothstep(reelDeadband 0.35, 1, depth)`: a light hold is a genuine pure turn; a deep squeeze pumps. Keyboard Shift is digital (1.0) — it holds, and the stick's Y is its winch. |
| Trigger release | — | LET GO: fling along the current velocity. |
| Both sticks (both arms free) | air control on the velocity, only from the component the two sticks AGREE on (`airControlDegPerSec` 75) — a single stick is aim, never steering, so aiming cannot move the cone the reticle lives in. | |

Aim frame: forward = course, up = WORLD up (the hull's up only when the course is near vertical).
The hull banks into every arc; a frame that rolled with it sent the other arm's resting reticle
up-and-left after a right-hand swing, straight past the next bough (measured in the ladder, §7).
Touch: no triggers → **unsupported** (documented, not worked around). AI: §6.
Note `VesselDamageBySkimmerEffect.asset` mutes `RightStickAction` for its `muteSeconds` — a Rhino
sword hit mutes the RIGHT arm's next cast. Intended counter-play, recorded here.

## 3. Physics (all in `MoveShip`; this ORDER is the contract)

1. **Sync external writes**: `SetInitialSpeed` (base `speed` changed → rescale v), a `Course` write
   (`SetPose`/`SetCourseVelocity`/the AI drift entry → re-aim v), an external `accumulatedRotation`
   write (`SetPose`, the spin doors → re-aim along the new nose; detected by EXACT component
   equality — `Quaternion.Angle` of two identical quaternions is not zero in float32 and fired every
   few frames, turning the winch's radial approach into tangential speed), and a POSITION teleport
   (> `(|v|·tm + |shift| + 2·reelRate)·dt + 1` → both lines wither, v re-aimed along the nose).
2. **Air control** (no line taut). 3. **Drag** `v *= 1/(1 + k·|v|·dt)` — the exact solution of
   dv/dt = −k v², bit-identical at 30/60/120 Hz. Never "simplify" to `v -= k v² dt`.
4. **Winch** per latched arm: `L -= reel·reelRate·dt` / `L += payOut·payOutRate·dt`, clamped to
   `[floor, maxLineLength]` where floor = `max(minLineLength 18, anchor size + hull radius + 2,
   |v_orbital| / maxSwingRate 4 rad/s)`. **The floor STOPS a reel, it never pays a line out** — the
   pump raises the orbital speed and with it the floor, so a line can legitimately sit below the
   floor of the moment; lengthening it there chattered between reel and pay-out at every frame rate.
   Two held lines stall at `L_L + L_R ≥ |A−B| + 0.5` (the spheres must keep intersecting) and hold
   the intersection circle above the same floor. **Zip**: a slack line being reeled pulls the hull
   toward the anchor — `approach = MoveTowards(approach, zipMaxApproachSpeed 320, zipAccel·reel·eff·dt)`,
   scaled by the SAME efficiency as the pump, so it can never out-run the cap (unscaled, a
   zip → pass the anchor → far-side snap → release loop reached 284 u/s with the pump never firing).
5. **Integrate the free case**: `pos += (throttleMultiplier·v + velocityShift)·dt` — the danger
   slow and knockback live in every state.
6. **Constraints.** No line taut: find the first slack line that goes taut along the chord
   (ray–sphere quadratic), SNAP there once, run the rest of the frame on the sphere. One line taut:
   the **sphere step** — in the ANCHOR'S frame (measure the radial against where the anchor WAS at
   the start of the step, rotate radial and tangential velocity together by
   `θ = throttleMultiplier·|v_t|·τ / h`, re-attach to where the anchor IS), winch `h0 → h1` with the
   closed-form pump, slide the knockback along the sphere, carry the anchor's velocity — exact
   circle at any dt, zero numerical dissipation, and swinging on a shark works (a release carries the
   shark's velocity). Two lines taut: the **circle step** — the intersection circle solved in closed
   form (`a = (L_L² − L_R² + D²)/2D`, `h_c = √(L_L² − a²)`), exact rotation about the anchor axis,
   the pump applied ONCE on `h_c`. A double catch on the far side of the chord is a **fling**, not a
   catch (a rope twangs; it never stops you dead).
7. **Speed floor** = the fleet's `MinimumSpeed` (10, authored on the prefab; `DefaultMinimumSpeed`
   when unset). From rest one full reel can raise it to at most `floor·(maxLine/minLine)^p` — the
   stranding rescue, stated and bounded. 8. **Publish**: `speed = |v|`, `Speed = speed·tm`,
   `Course = v̂`, NaN guard (a bad frame re-aims along the nose and drops the lines rather than
   reaching the speed tunnel, the HUD and replication).

**The snap.** Outward radial part removed; the tangential part keeps
`lerp(snapRedirectHeadOn 0.35, snapRedirectGlancing 0.95, cos²θ)` of the whole — a glancing grab
keeps speed, a head-on grab pays, and if the tangential part is under `lineBreakTangentFraction`
0.1 of the whole the line BREAKS (released, speed kept, tempo reset). The kick scales with the
centripetal onset `v²/L` over `snapShakeAccelRef` 600 — how hard the swing genuinely is — never
with the removed radial speed, which is largest for the worst grab.

**The pump.** The exact solution of `dv/d(ln h) = −p·v·(1 − v²/c²)` from `h0` to `h1`:
`v1 = c / √(1 + ((c² − v0²)/v0²)·(h1/h0)^(2p))`, `p = min(1, pumpGain 0.35·(1 + tempo·0.12))`,
`c = pumpSpeedCap 320`. p = 1, c → ∞ is v·h = const. Reeling in raises v, paying out lowers it,
v never crosses c from below, and a hitch frame is exact. Only a line that was TAUT when the winch
turned is credited — a slack line's shortening is take-up, not work.

## 4. Tempo and the slingshot

Tempo climbs on the RELEASE of a grab that (a) swept ≥ `tempoMinSweepDeg` 30, (b) was latched
within `tempoWindow` 0.9 s of the previous release, (c) on the OTHER arm — rhythm is swings that
carried, which is brachiation; a flutter-tap counts nothing. A whiff, a broken line or a coast
longer than the window resets it. It raises the pump exponent toward — never past — the
conservation law, and reads in the world: the line thickens with it.
Slingshot: letting go of the SECOND of two held lines within `slingshotWindow` 0.2 s of the first,
past the chord between the anchors, pays `slingshotBonus` 1.15 scaled by the winch's efficiency —
a timing reward on speed the winch built, never a source that compounds (the fuzz found 7,208 u/s
before the efficiency scaling).

## 5. Hull, camera, visuals, juice

- `RotateShip`: `LookRotation(course, up)` at `noseTrackDegPerSec` 540 (above the 229°/s swing
  ceiling), `up` banked toward the anchor by `bankIntoSwing` 0.6 while taut, settling to world up
  at `uprightSettleRate` 1.5/s. `accumulatedRotation` mirrors it (the spin doors stay valid).
  Base gyro and `TurnScalar` never apply; the nose keeps tracking velocity while
  `IsTranslationRestricted` (harmless, stated).
- Camera (`SpiderCameraSettingsSO`): `mode 1` DynamicCamera, `dynamicMinDistance −80` (negative =
  behind; the shipped `+15` was the wrong sign — `followOffset.z` is ignored in this mode, `z` IS
  `dynamicMinDistance`), `followSmoothTime 0.12`, `rotationSmoothTime 6`, adaptive zoom OFF and
  `adaptiveMaxDistance 0` (the old 250 claimed the fleet's widest camera for nothing). No FOV
  writes: the speed tunnel drives FOV from `Speed`, which the Gibbon publishes.
- Visuals are runtime primitives: a lit HDR-cyan capsule per line (thinner while slack, thickens
  with speed and tempo, a kill pulse on a sweep), a hand capsule from the hull to the reticle while
  the arm is free, and a sphere reticle — the AIM reticle while free (snapped onto the locked
  prism: bright and 1.6×; dim at max range otherwise; scaled with distance so it reads at 170u),
  the FLING reticle while latched (`pos + v·0.6 s`: thread it through the next hoop). Everything
  blooms in and withers out; a released line retracts, a whiff retracts at cast speed.
- Juice: snap kick, release kick (∝ speed), sweep-kill tick — local human only, via
  `CustomCameraController.Shake`. **No haptics** (fleet policy). Audio: six `EventReference`
  slots shipped EMPTY (`castEvent`, `snapEvent`, `releaseEvent`, `whiffEvent`, `tempoUpEvent`,
  `slingshotEvent`), silence never substitution.
- The sweep is kept: a TAUT line slices prisms it sweeps through (`PrismSpatialIndex.QuerySegment`,
  anchors and super-shielded mass exempt) — an active force.

## 6. AI

The transformer drives its own arms under autopilot from `AIPilot.CurrentTargetPosition` (a new
one-line accessor — AIPilot only ever writes the stick sums, never a trigger): cast the arm on the
side of the turn (or alternate when the objective is dead ahead) at the resting yaw, reel at
`aiReelDepth` 1, release when the velocity is within `aiReleaseAngleDeg` 12 of the objective, or
after `aiMaxSwingDeg` 150 / `aiMaxSwingSeconds` 2.5. On the autopilot RISING EDGE both arms are
dropped silently — a human-held trigger never delivers its release under autopilot. `Spider.prefab`
authors `breakOrbits: 0` (the orbit-break's turn radius is not a brachiator's) and
`PitchScaler/YawScaler 229` (= `maxSwingRate` in deg/s) so `MinTurnRadius = v/ω` is the true line
floor for anything that still reads it.

## 7. Offline proof (the harness is the shipped C#, not a mirror)

`scratchpad/harness/`: Unity-shaped stubs with real math drive `SwingingVesselTransformer` through
`Update()` by reflection. 47 assertions, all green: T1 drag closed form, rate-identical · T2 full
reel matches the logistic at 30/60/120 Hz (1%), `v·h` invariant at p = 1, nothing above the cap ·
T3 taut swing: |v| bit-constant 10 s, sweep = vT/h, ONE snap, on the sphere · T4 one snap at the
edge, post-snap speed rate-identical, head-on BREAKS · T5 200k fuzzed frames (random casts,
releases, sticks, a 1/20 hitch frame): no NaN, `|v(n+1)| ≤ max(|v(n)|·bonus, cap)` · T6 dual
feasibility stall, double zip bounded by the cap · T7 scripted tape 60 vs 120 Hz within 1% speed /
2° heading / 2% path · T8 towed anchor: `|Δpos/dt − v| < 8 u/s`, a 40 u/s shark never breaks the
line, a 200u jump does · T9 throttle 0.5 halves the arc, knockback keeps `|d − L|` and `|v|` · T10
`SetInitialSpeed` / `Course` / teleport adopted · T11 nothing past the floor. The FEEL ladder
(five alternating grabs from 100 u/s, release at 60° swept): **100 → 133 → 158 → 178 → 202 → 222**,
tempo 0 → 4, every beat under a second and shortening as the rhythm builds.
Three defects the harness caught in the FIRST pass, all invisible to review: the noise-triggered
re-aim (§3.1), the tow measured against the anchor's NEW position (§3.6), and the floor paying a
line out (§3.4).

## 8. In-editor verification (this session had no editor — `/verify-unity` was not run)

1. Open `Spider.prefab`: `SwingingVesselTransformer` shows the new headers; `R_VesselActionHandler`
   binds InputEvent 2 → `GibbonLeftArmAction`, 1 → `GibbonRightArmAction` (Boost and the old
   SwingAction are gone); `AIPilot.breakOrbits` 0; scalers 229; `MinimumSpeed` 10.
2. Freestyle: pick the Gibbon in the vessel changer. Two reticles lead the hull at ±61°-ish off
   course; one brightens and grows when it sits on a prism. LT/RT casts, the line blooms out, the
   SNAP kicks the camera once, holding reels (the line shortens, speed climbs, the hull banks),
   letting go flings. Stick-down on a held arm slows you (pay-out). Whiff retracts fast.
3. Keyboard: LShift/RShift cast and hold; W/S on the matching stick reels/pays out.
4. Ability lockup: the Charge and Mass cards draw LT / RT chips; Space and Time read LOCKED.
5. Camera: behind and above at 80u with visible lag through a swing; FOV narrows with speed.
6. Danger prism: the slow applies mid-swing (arc shortens); a Rhino sword hit mutes the right arm
   briefly.
7. Netcode (two clients): a remote Gibbon moves; its lines/reticles are NOT drawn (open item).
8. Canopy Run: `CANOPYRUN.md` §5.

## 9. Open items / not done

- Lines and reticles are not replicated to peers (the hull's motion is). `NetEchoSightShape` is
  the precedent for replicating a few scalars through `R_VesselActionHandler`.
- A client's cast is one RTT behind the press (ServerRpc→ClientRpc, like every ability); the reel
  is local. `tempoWindow` 0.9 s assumes ≤ 100 ms RTT.
- `Resources/ElementalAbilityMaps/Gibbon.asset` binds the two ARMS (real inputs, chips drawn) and
  leaves Space/Time as OPEN DESIGN SLOTS. Proposal, not authored (needs sign-off): Space → reach
  (`maxLineLength`/`castSpeed`), Mass → winch torque (`pumpGain`/`zipAccel`), Time → rhythm
  (`tempoWindow`/`tempoMax`), Charge → the blade (`sweepBladeRadius`) — every element changes the
  BEAT, not a stat. No `GibbonHUDVariant` exists; the nested `SquirrelHUDVariant` carries the row.
- Touch is unsupported (no triggers).
