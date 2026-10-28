# Stoat — the Field Dipole (Space), the Pathfinder (Time), the Mode switch (Mass) and the Ripple (Charge)

The round-15 Stoat, ported from the Stoat Flight Studio's field trajectory
(`Docs/Studios/StoatFlightStudio.html`, the `ft*` row; `Docs/Studios/README.md` round 15), and since
**2026-10-13 its own vessel**: all four abilities and all four level-5 upgrades authored, and nothing of the
Squirrel it was cloned from left on the prefab (§1a–§1e, §4). **Time mode** (2026-10-14) is the studio's
round-6 dipole sling: a light pole laid ahead that CATCHES you onto an orbit, drawn as two rings (§1b).

**Quick read — the four abilities.**

| Element | Ability | Input | What Space / Time / Charge / Mass scales | Level 5 |
|---|---|---|---|---|
| **Space** | Field Dipole (Mass mode's triggers) | LT / RT | the pair's reach: pole size ×1 → ×2 (and the orbit's radius ×1 → ×1.4 in Time mode) | **Accretion** — swallowed prisms are stolen and grow the poles |
| **Time** | Pathfinder (passive) + Time mode's timed slingshot | — | the boost: warp ×2.5 → ×5, and the slingshot's kick by the same ratio | **Foresight** — rivals' predicted paths |
| **Charge** | Ripple (passive, on every annihilation) | — | the ripple's reach ×1 → ×2 | **Siphon** — stripped petals come to you |
| **Mass** | Mass / Time Mode | B | Mass mode's trail thickness ×3 → ×6 | **Armoured Wake** — Mass-mode prisms arrive shielded |

## 1. How it flies

**Space — the Field Dipole (LT / RT).** Squeeze a trigger and ONE sink–source pair (a black hole and a
white hole, each the other's throat) is laid **250 u** ahead, the poles together, in the frame the hull
had then. The triggers' **difference** pulls the poles apart sideways — the sink on the deeper
trigger's side — and their **sum** pulls them apart lengthways, the sink always the nearer pole:

| triggers | separation (sideways, lengthways) | reads as |
|---|---|---|
| one full | 200 u, 60 u | the sink to that side, a slight diagonal |
| both full | 0, 120 u | the sink dead ahead, the source 120 u beyond it |
| let go of either | → 0 | the poles **slam together** and **neutralize** — never caught again |

**Every press lays a fresh pair** (replacing any pair the other trigger still holds; both triggers
pressed on one frame lay one), and a released pair is gone from your hands at once: a trigger still held
after the other is let go keeps its squeeze but lays nothing until it is pressed again (2026-10-13, play
feedback: "when I let go I should have no chance of recovering those poles").

**The annihilation (2026-10-13).** Released, the pair belongs to no trigger: its poles accelerate into
each other from where they were let go (`StoatDipoleMath.SlamRemaining`, `1 − (t/T)²` over
`annihilateSeconds` = 0.3 s, so they hit at full speed) and both holes' `BlackHole.Amplitude` falls with
the separation — a dipole's field IS its separation — so their pull, their tides on prisms and their lens
cancel as the poles meet. The lens follows the amplitude at the holes' FULL size (`_BHStrength` in
`BlackHoleLens.shader`: the bend relaxes toward straight, the dark shadow and the light core fade into the
scene behind them), so two opposing fields neutralize into nothing instead of two warps shrinking. The
holes despawn at amplitude 0, where the despawn's own shrink is invisible. The owner's flight ignores a
released pair. Every peer runs the same slam on its copy (`StepCollapses`). A press while a pair is still
slamming takes the budget from it, oldest first.

The poles' strength and size never follow the triggers — only their separation (the dipole moment)
does. Space sizes them: horizon **2.1 u** and GM **50 000** at rest (the studio's 3.5 u and 120 000;
cut to 1.17 u / 60 000, then 1.75 u / 40 000 "a bit bigger and weaker", then on 2026-10-13 "a little bigger
and a little more powerful"), ×1 → ×2 at level 10
(`StoatDipoleConfigSO.poleSize`, read live on the owner, replicated as the horizon). The sink grows in
over 0.9 s, the source over 1.1 s (from 55%), so a fresh pair pulls before it pushes.

**The field** on the owner's flight and the pathfinder: the sink's pull **softened over a 14-horizon core**
(`sinkSofteningHorizons`; ε = 29.4 u at rest) — `GM·r/(r² + ε²)^1.5`, the source's own law — and the
source's push `GM·r/(r² + ε²)^1.5` (ε = 2 source horizons), the sum capped at 3000 u/s². The studio's
steep Paczyński–Wiita `GM/(r − r_s)²` made a near pass all-or-nothing: measured unsteered at 30 u/s,
offsets 8 u apart bent 2°, 63°, 166°, 179°, 150°, 71°…; softened, the same passes bend smoothly 29° → 141°
→ 75° across 0–120 u at the shipped 2.1 u / 50 000 (9° → 25° → 12° at 60 u/s), the widest forgiving band of
warped lines of the candidates tried (prisms and rival
vessels still feel the platform's own law). One step of flight in it
(`StoatDipoleMath.Step`, the studio's `fieldSubstep`) turns the nose by the field's perpendicular part
(capped at 12 rad/s) and carries a **gravity speed** along the nose from its parallel part (gripped back
toward cruise at 0.5 /s, ceiling 4 × cruise). Fly into the sink's horizon and you come out of the source
on the side opposite to where you went in, just outside its horizon (`StoatDipoleMath.ExitPoint`), with
the gravity speed you had before the dive.

**The fly-through ZONE (2026-10-13).** The drawn horizon is small, so the sink also takes a hull that
LOOPS it: within **135 u** of the sink (× the poles' size, `throughZoneRadius`; 90 before the poles grew
1.5×) every turn of the heading is counted — toward the sink adds, away subtracts, floored at 0, cleared on
leaving the zone — and at **200°** (`throughDegrees`) you go through (`StoatDipoleMath.WindThrough`, shared
by the flight and the pathfinder, so the line shows it). A U-turn round the attractive pole is yours; a
loop past 200° is taken. Measured headless on the shipped field: at 60 u/s a pilot holding the sink abeam
turns ~180° and flies out, steers out of it, or keeps orbiting and is taken at 200°; at 30 u/s steering
out after 170° escapes, a centred stick lets the pole carry you past 200°, and letting go always frees you.
135 u is the smallest zone that holds a stick-held orbit at 60 u/s (at 90 it left the zone and reset).

**Time — the Pathfinder (passive).** Every frame the hull's own flight is run forward 600 u on the
inputs it holds (`StoatDipoleMath.PredictPath`, the studio's `fieldPath`: the same `Step`, the held
steering rates, through the wormhole) and drawn for the pilot as dots from 8 u ahead of the nose. Since
2026-10-13 it flies the hull the way `VesselTransformer` does (`StoatDipoleMath.HullResponse`): the stick
turns the COMMAND and the hull slerps after it at `RotationFollowRate` (1.5 /s), and the engine eases to
`CurrentThrottleTarget` at the same rate. The first port turned the body at the stick's rate at once and
held its speed, which put the drawn line 15–75 u off the hull's real path within 3 s of starting a turn
(measured headless) — "the vessel doesn't take the path". The line runs **1200 u** (two to three windings
of a held helix):
4 px dots, 2.5 diameters apart (the studio's 10, cut on 2026-10-13), **blue-grey** while open. The dots
carry a light **depth** cue: each is sized `sqrt(nearest dot's depth / its depth)`
(`StoatDipoleMath.DepthScale`, floored at 0.75), its gap is measured in its own diameters, and it fades
by up to 15% toward the far end (`dotDepthMinScale`, `dotDepthFade`) — light enough that the far windings
of a helix stay legible behind the near ones. When the poles **warp** the path at all — it
bends ≥ 3°, or goes through the wormhole — it turns **lime** and the hull flies down it faster: a warp of
the hull's own flight clock (`VesselTransformer.FlightTimeScale`), so the path stays the path and only
arrives sooner. The boost rises at 20 /s and fades over 0.6 s. **Time is rate:** ×2.5 at rest, ×3.75 at
level 5, ×5 at level 10 (`StoatDipoleConfigSO.boost`, floored at 1; the studio's ×2 → ×4 raised a quarter
on 2026-10-13). A path that comes back
within 8 u of itself (at least 60 u on) ends there — the loop that would cross your own trail.

**The base turn is ×0.4** (pitch / yaw 48 °/s, roll 52 — the studio's `ftTurnScale`): the hull leans on its
poles to turn, a held full turn draws a circle the pathfinder does not call warped, and the line is
not twitchy on screen. **X** still holds the hull still (the Sparrow's stop), outside the four abilities.

**The autopilot circles its sink at most ONCE** (2026-10-10). The path-watching hold keeps the poles open
while they warp the predicted path — and a hull ORBITING its own sink keeps that path warped every frame,
so the hold never dried out and ended only at `autopilotMaxHoldSeconds` (15 s): reported in play as the AI
going round inside the black hole's shadow four, five, six times before it got out. The executor now sums
the angle the hull sweeps round its sink (`StoatDipoleMath.SweptAround`, skipping a frame that went through
the wormhole) and lets go so the lap ENDS at `autopilotMaxOrbitDegrees` (**360** = one lap, 0 = no cap), in
either hold mode. "Ends" matters: the poles keep pulling until they close (`FollowRate`), and letting go AT
360 left the hull another half lap round the closing pair, so the sweep still to come
(`ClosingSeconds` × the smoothed orbit rate) is counted at the moment of the decision (`OrbitCapReached`).
Measured in the Stoat Flight Studio (same AI, same closing law, Hard, one race per intensity): the most a
pair was circled went **7.43 → 0.66 laps** at I2 and **3.23 → 0.60** at I3 (I1 and I4 never orbited: 0.55,
0.77), race times unchanged within a second (58.5 → 59.6 s, 51.8 → 51.5 s). After a capped let-go it waits the full `autopilotIntervalSeconds` rather than the 0.25 s relay, so
the next pair is not laid while the hull is still turning out of the last one. A pass by the sink sweeps
under 180°, so the cap never cuts one short (`StoatDipoleTests.OrbitCap_*`).

## 1a. The modes — B switches Mass and Time (Mass, 2026-10-13)

The design call: *"a mass mode that puts out a thicker trail and uses the current behavior. then it can switch to
a time mode where it restores the auto orbit behavior we were testing before that boosted players if they timed the
orbit near 90 degree orbit rotation."*

| | Mass mode (the default) | Time mode |
|---|---|---|
| **LT / RT** | the Field Dipole (§1) | the catch slingshot (§1b) |
| **What is drawn** | the pathfinder's predicted path (§1) | the pole's DETECTION and ORBIT rings — no path (§1b) |
| **The poles** | horizon 2.1 u, GM 50 000 (× Space) | LIGHTER: horizon 1.6 u, GM 20 000 |
| **The trail** | THICK: the prism's height × `massModeThickness` (Mass ×3 → ×6, read at the REPLICATED level) | as authored |
| **Mass 5** | every prism arrives SHIELDED (Armoured Wake) | — |

**B** (`InputEvents.Button2Action`; T on a keyboard, the Vessel Studio's key too — R until 2026-10-14) steps the mode,
**Mass → Time → Gate → Mass** since 2026-10-26 (`StoatModeActionSO` → `StoatModeExecutor.Toggle`; Gate is §1f).
It is a press with no release semantics (vessel rule 37). Both trigger assets ride each trigger (`StoatDipoleLeft/
RightAction` AND `StoatSlingLeft/RightAction`); each executor stands aside outside its own mode, so the binding never
changes and the peer-press harness sees one ordinary binding. **A switch lets go of whatever the triggers held** — the
dipole annihilates where it stands (and throws its ripple), a Time-mode pair is let go WITHOUT its kick — and a trigger still
held after the switch does nothing until it is pressed again.

**Where it runs.** Every peer: the press round-trips like every ability press, and the mode both routes the trigger
presses that follow and decides the trail every peer lays (the Butterfly's `SpreadWingsActionExecutor` shape).
The thickness eases over `blendSeconds` (0.8 s) and is written every frame to
`VesselPrismController.ThicknessMultiplier` — a new sibling of `WidthMultiplier`, admitted past the pool's scale
window the same way (`AdmitTargetScale` after `Initialize`).

**The autopilot switches too** (never assume an AI can use a human's input): Time mode when its target is
≥ `autopilotTimeModeDegrees` (70°) off the nose — an orbit is a tight turn — and back to Mass mode within half of it,
at most every 3 s, never while a trigger is held, through the replicated press.

## 1b. Time mode — the catch and the 90° slingshot (2026-10-14)

The design call, after the first cut was played: *"Mass mode [should] be like what we had before I tried to split into
modes. Time mode would have less mass and reflect the way we had it working before the pathfinding and instead of
showing your path it shows a detection radius and an orbital radius. then after orbiting 90 degrees it grants a timing
bonus upon releasing near 90. so this one can not suck the player in because it always catches them."* And of the first
cut: *"When I switched modes I couldn't see the poles at all and was just pulled right or left."*

**What was wrong with the 2026-10-13 cut.** It restored the round-4 orbit (`STOAT.md` §1): the attractor laid SQUARE
BESIDE the hull at the orbit radius (150 u to the side with the chase camera 21 u behind — ~80° off the view axis, off
screen) and the orbit started on the press, so the pilot felt a pull to one side and saw nothing; its strength was the
circular-orbit GM (≈375 000 for a 150 u orbit at 60 u/s), several times Mass mode's poles.

**What it is now — the studio's round-6 dipole sling** (`Docs/Studios/StoatFlightStudio.html`, `layDipole` /
`holdDipole` / `releaseDipole`, the `dp*` rows), with the 90° timing on top (`StoatSlingExecutor`, `StoatSlingMath`,
`StoatSlingConfigSO`):

| | |
|---|---|
| **Press** | a LIGHT pair is laid **`catchAhead` 150 u ahead**, the attractor **`catchSide` 45 u** to the pressed side, the repulsor mirrored (`StoatSlingMath.CatchPair`): in front, where you see it. Horizon `poleHorizon` 1.6 u, GM `poleGM` 20 000 (Mass mode's poles are 2.1 u / 50 000). It does not move and pulls no vessel. |
| **The rings** (in place of the path) | round the attractor, in the plane of your pass (`StoatSlingMath.RingNormal` / `Ring`), drawn as world dots: the **ORBIT radius** (the squeeze, read live: a touch 150 u → buried 40 u, × Space 1 → 1.4) and, fainter, the **DETECTION radius** (`catchReach` 1.5 × the orbit). The orbit ring brightens once it has caught you, eases toward lime as the quarter-turn nears and is **lime inside the window**. |
| **The catch** | as you pass it — inside the detection radius with the attractor ≥ `catchDegrees` 50° off the nose — or as soon as you are inside the orbit radius (`StoatSlingMath.Catches`). A straight pass is caught at every squeeze (45 / sin 50° = 58.7 u ≤ 1.5 × 40 u; `StoatSlingTests.Catches_AStraightPassAlways_AtEveryShippedSqueeze`). Once caught the nose eases onto the tangent (`settleRate` 3 /s, `StoatSlingMath.SettleHeading`) and the hull is held on the orbit radius. |
| **It cannot suck you in** | it catches you at the orbit radius (≥ 40 u, 25 horizons out), an owned pair pulls no vessel (`BlackHoleVesselPull`), and there is no field flight in Time mode. |
| **Release** | the slingshot out along the tangent, the kick TIMED by the angle actually swept round the attractor since the catch (below). Let go before a catch: no kick. Past `maxOrbitDegrees` (360) you are let go on your own (through the replicated release). |
| **The pair** | falls together at `driftSpeed` 40 u/s and annihilates; the meeting throws the Charge ripple (§1c). |

```
kick = SlingBoost(speed, deepest squeeze) × lerp(missKickShare, 1, SweetSpot(degrees swept since the catch)) × Time's boost ratio
SweetSpot = 1 within ±15° of 90°, smoothstep down to 0 over the next 30° either side
```

So a release a quarter-turn round (out on a tangent square to the heading you came in on) pays the whole kick; a tap
release or a half orbit (180°) pays a fifth (`missKickShare` 0.2). **Time is the boost**: the kick scales by
`StoatDipoleConfigSO.BoostRatio` (×1 → ×2). A kick past the shared 100 u/s displacement ceiling raises the cap for its
own life (`ModifyVelocity(…, ceiling)`, vessel §4.ab2). The HUD's Time card fills toward the sweet spot and turns lime in
the window. `sweetSpotEvent` (empty) plays on a timed release. The kick is a share of the ENGINE's speed (the published
speed with the warp taken out, as the studio's `speed + flow`), so the warp below paces the flight without compounding
into the kick.

**The pace (2026-10-14, the studio's `dpWarp` / `dpWarpHeld`).** Play feedback: "the slingshot orbit is pretty slow
compared to the field trajectory … the vessel feels better when it's moving fast." Time mode now runs the field's own warp
rule with "the pair is held" in place of "the path is lime": from the press (`catchWarpFromPress`, or only once caught)
the hull's flight clock (`VesselTransformer.FlightTimeScale`, still written only by the pathfinder) rises to
`1 + (Boost − 1) × catchWarpShare` at `boostRise` and fades over `boostFadeSeconds` after the release. Decided in the
artifact's batch race (the AI at Hard, best of three squeezes; the field ×3 boost):

| | Circuit best / median | Hairpins best / median | Avg speed (circuit) |
|---|---|---|---|
| Field trajectory (Mass) | 59.6 / 59.6 s | 33.2 / 33.2 s | 131 u/s |
| Catch, round 6 (no timing, no warp) | 107.2 / 111.8 s | 110.1 / 116.0 s | 75 u/s |
| Catch, 90° timing, no warp (what shipped 2026-10-14) | 115.8 / 122.6 s | 138.0 / 149.5 s | 66 u/s |
| **Catch, timing + warp from the press, share 1.15** | **56.1 / 61.2 s** | 71.4 / 79.5 s | **122 u/s** |

The circuit is now level; the hairpins are still the field's (the catch's circles cost time there — tuning, not pace).

**A controlled orbit (2026-10-27).** Playtest: *"going near the black hole I get shifted right or left toward the side
of the white hole; I cannot go in the middle."* Measured in the studio: a deep squeeze was caught ~120 u out and held on a
126 u circle round a black hole only 90 u from its white hole, so the orbit reached round the white hole and swung the
hull past its side, and the catch snapped the hull onto the squeeze's circle wherever it was (Unity's shape: a light
squeeze = a 150 u circle round the 90 u pair). Two knobs, studio and Unity alike: **held where it caught you**
(`holdAtCatch`, on; the held circle eases to the squeeze's at `orbitEaseRate` 0.8 /s) and **the circle clear of the white
hole** (`whiteClear` 0.8: at most 0.8 × the poles' distance, 72 u; `StoatSlingMath.ClearOfRepulsor`). A pilot aiming
beside the black hole is now caught at ~68 u (was 120), 9/9 passes caught, no dives; the AI's Time races 56.1 → 53.8 s
(circuit I2), 80.4 → 76.5 s (hairpins I2). Measured and left off in the studio: `dpCatchInside 0` (catch only as you pass)
dives you into the hole when aimed straight at it.

**The autopilot** presses the trigger on its target's side (target ≥ 30° off the nose, ≥ 120 u away, every 3 s), flies
toward its target past the pole and is caught; once caught it lets go when the target is within `aiReleaseConeDegrees`
(20°) of the nose, or within 60° inside the timing window; a pair that has not caught it in `aiCatchGiveUpSeconds` (4 s)
is let go. Both edges through the replicated press.

**Where it runs.** The press and release replicate: every peer lays its own copy of the pair at the replicated pose and
lets it go on the release. The catch and the orbit fly on the simulating machine; the rings are the local pilot's.

## 1c. The Ripple — Charge, passive

The design call: *"charge should always be focused on anti vessel/fauna gameplay. the explosion is cool but it should
only kill fauna and strip elements from vessels. and it should look like a cool space time ripple."*

**Every annihilation** of this hull's pair throws a ripple from where the poles met: a dipole let go (or replaced by a
fresh press, or cut short by the hole budget), a Time-mode pair closing after its release (`BlackHoleRegistry.PairMet`),
or a fresh Time-mode press over a pair still closing. Its front expands with an ease-out over `expandSeconds` (0.9 s) to

```
radius = clamp(separation at the let-go × 0.6, 30, 160) × Charge (×1 → ×2, at the REPLICATED level)
```

so a pair held wide is a wide ripple — the dipole moment becomes the threat. As the front sweeps over them:

| reached by the front | what happens |
|---|---|
| a **rival vessel** | stripped of **one whole petal of every element** it holds (`ResourceSystem.AccrueElementalLoss`, classed `ElementalDebuffSources.Explosion`, so a ward against blasts stops it), shed as crystals around it — with **Charge 5 — Siphon** the petals go straight to the Stoat (`GrantPetals`) |
| a **creature** | killed (`Fauna.Predated`, the platform's death: heart released, mass conserved) on the fauna's simulating machine |
| a **prism**, the owner, a teammate | nothing |

Each victim is struck once per ripple. **Where it runs:** every peer, from its own copy of the pair (each peer's copy
collapses / meets); the strip runs on each peer's copy of the rival's petals, the per-peer stance the wormhole toll and
the sink's own crystal strip take, with Charge and Siphon read off replicated state so every peer sweeps the same sphere.

**The picture** (`StoatRipple` + `StoatRipple.shader`): a back-faced sphere drawn by the black-hole lens pass
(`LightMode = BlackHoleLens`) from its copy of the camera colour. Per pixel, the view ray's closest approach to the
centre picks the ring the shell projects to; within a band either side the ray is BENT — toward the centre outside the
front (the scene pushed out), away inside it (pulled in), a derivative-of-Gaussian profile — with a red / blue split
and a thin rim in the owner domain's light colour, fading after the front settles. A ripple holds the lens pass open
(`BlackHoleLensPass.Acquire`) so it draws whether or not a hole is still live. Stated limits: no depth test (an opaque in
front of the band is bent with the background for the wave's ~1 s); a camera inside the front sees nothing.

**Subtle, and the ONLY annihilation effect (2026-10-14).** Play: *"the effect that was supposed to follow the attractor
repulsor annihilation is occurring at the creation of the poles instead. it is also far too big and bright … it should
just be the subtle ripple after an annihilation."* What fired at CREATION was the studio's white-hole birth light
(`BlackHoleWaves.Kind.In`: a 700 u shell converging on a fresh pair, washing the screen as it crossed the camera,
ported 2026-10-12), and at the meeting its outgoing light shell and gravitational wave (with a camera shake) stacked on
the ripple. All three are now behind `StoatDipoleConfigSO.annihilationWaves`, **off**; nothing is drawn at a pair's
birth, and the meeting draws this ripple alone, toned down: `bend` 0.025 → **0.008**, `chromaticSplit` 0.12 → **0.03**,
`rimGlow` 1.6 → **0.15**, `bandWidth` 0.12 → **0.08** (`author_stoat_assets.py` owns `StoatRippleConfig.asset`). Its
REACH (what it strips and kills) is unchanged.

**The death waves are back, the birth wave stays off (2026-10-28).** Root cause of the "far too big and bright" flash:
the studio (three r128) adds a light shell's glow (0.35 × its envelope) and its screen wash (≤ 0.3) to its FINAL picture
in display values — an 8-bit target, no bloom, no tonemapping. Unity added them in `BlackHoleLensPass`, BEFORE
post-processing, as linear HDR light; the gameplay profile's bloom (threshold 0.2, intensity 2.5) then blew them into a
screen-filling white flash. The waves' light is now its own shader pass (`BlackHoleWaveLight`, pass 1 of
`BlackHoleLens.shader`), drawn at `AfterRenderingPostProcessing` and added in sRGB: the two passes sum to the studio's
composite (`Tools/Shaders/verify_black_hole_lens.py`, 1b). Every studio wave value is unchanged (`gwAmp` 12, `gwSpeed`
120, `gwPeriod` 0.35, `gwRingdown` 1, `gwPrism` 0.6, `gwShake` 1, `gwGlow` 1, `whInFrom` 700, `whOutSpeed` 450,
`whOutFade` 1.6, `shellBend` 6, `dieS` 0.7). `annihilationWaves` is **on**: a meeting throws the gravitational wave
(with its camera shake) and the outgoing shell, alongside the Charge ripple (kept: the ripple is the gameplay strike and
the waves are the studio's look; they do not duplicate). The CREATION wave (`Kind.In`) has its own switch,
`StoatDipoleConfigSO.birthWave`, **off** (the studio always fires it and has no switch, so `stoat.json` maps nothing).
Note: a fresh press over a live pair annihilates the old pair (0.3 s collapse), so its death waves leave near the new
pair just after it appears — the old pair's annihilation, not a birth flash. Stated limit: a hole's own ring glow and
core are still drawn before post-processing, so they still bloom.

**The Mobile look — the ripple finally shows, nothing warps (2026-10-29).** Yash: *"make sure we are not warping space
… make the shaders mobile friendly … the same [as the Meerkat] with these black / white hole pair."* The sphere above
was never drawn: its only pass is `LightMode = BlackHoleLens`, and no renderer list draws that LightMode (the lens pass
draws its own full-screen material, not the ripple's). With `BlackHoleConfig.look` = `Mobile` (the default,
`Docs/BLACK_HOLE.md` §5.4) the ripple draws its front as a thin soft light ring in the owner domain's colour
(`BlackHoleGlow.DrawRing`: brightness `rimGlow` 0.15, half width ½ · front · `bandWidth` 0.08, at least 0.5 u, faded by
the ripple's own strength), once a frame, with no copy and no depth texture. `bend` and `chromaticSplit` are kept for
the Studio lens look and unused by Mobile (their tooltips say so). The pair itself is a dark core with a thin photon
ring (black) and a bright soft core (white); the annihilation throws a faint local ripple (the gravitational wave's
own waveform, gone by `mobileWaveReach` 220 u) instead of bending the view; the hull no longer stretches near a hole,
and `gwShake` stretches nothing. The REACH, the strip and the kill are unchanged.

## 1d. The HUD — four cards, four gauges

`StoatHUDVariant.prefab` (a true variant of `VesselHUDPrefab`, copied once from the Thresher's: authored petal bars, a
four-card row on known fileIDs), `StoatHUDView`, `StoatHUDController` (every signal polled from the Stoat's OWN
executors, serialized on the prefab — rule 14):

| Card | Icon | Gauge |
|---|---|---|
| Charge | Ripple | the ripple a let-go would throw NOW, as a share of the largest |
| Mass | Mass / Time Mode | the switch: full in Mass mode, empty in Time mode, eased between |
| Space | Field Dipole | Space-5 Accretion: how far the open pair has grown |
| Time | Pathfinder | Mass mode: the warp boost on; Time mode: progress to the 90° sweet spot, lime in the window (the warp runs in both); Gate: a jump's warp as a share of its top, ready past half |

The Mass card reads the mode: full in Mass, **half in Gate**, empty in Time, and its **icon follows the mode**
(2026-10-26, `StoatHUDView.SetModeIcon`): `Stoat_ModeMass` (the thick trail and its path), `Stoat_ModeTime` (an orbit
round a solid attractor), `Stoat_ModeGate` (an arrow into a black hole, its white hole far along the line), placeholders from
`author_stoat_icon_placeholders.py` (`--check`) wired on `StoatHUDVariant.prefab`; an empty slot shows the card's own icon.

**On a phone** (Android, iOS: `KitInputDevice.IsTouchPrimary`, and only while no pad is connected; never on PC or
Steam) the Stoat flies the TotemButtons profile (`author_touch_profiles.py`): the cards round the right thumb are its
buttons. **The Mass card is B** (its ability's Input 7, `Button2Action`, routed to `StoatModeAction` by the prefab's
`_touchActionOverrides`), so a tap steps Mass → Time → Gate and the icon shows where you are; the **Space card is LT**
(the left dipole, the left catch in Time, the gate in Gate mode), the **Dipole disc is RT** and the **Hold disc is A**.
`StoatSlingTests.Touch_TheModeCardIsTheModeSwitch_OnAPhoneToo` pins the binding. Open: the Dipole disc and the Space
card keep their dipole icons in Time and Gate mode.

The icons are placeholders (`Tools/Build/author_stoat_icon_placeholders.py`, `--check`); the card icons on
`SO_Class_Stoat` are renders of the Stoat's own hull (since 2026-10-18 its model: `Tools/Build/vessel_model/render_cards.py Stoat`, `--check`).

## 1e. Space 5 Accretion, Time 5 Foresight, the omni crystal

- **Accretion (Space 5, with Theft fused in — rule 28).** Prisms the sink takes are STOLEN (unchanged: out of the
  source in your domain) and every one grows BOTH poles `accretionPerPrism` (4%) of their size, to `accretionMax` (×2),
  until the pair annihilates. Counted off the sink's new `BlackHole.CapturedCount` (the gravity field counts every
  capture: vanish, consume, throat transit), on the authority; peers see the grown size as the published horizon.
- **Foresight (Time 5).** The pathfinder also draws the paths of the `foresightMaxRivals` (3) nearest rivals within
  `foresightRange` (1500 u), `foresightLength` (500 u) long, in their domain colours: each flown forward on the turn
  and speed it is holding NOW — its rotation's change since last frame, in its own axes — through the same
  `StoatDipoleMath.PredictPath`. Rivals rescanned once a second. Local pilot only; presentation only.
- **Overclock (the omni crystal, proposal O1).** `StoatOverclockByCrystalEffectSO`, the only Stoat entry in
  `vesselCrystalEffects`: 6 s in which the boost holds full and the line stays lime, warped or not.

## 1f. Gate mode — the warp gate (2026-10-27)

**History.** B's third press was first Hybrid (2026-10-26: the orbit with a half-length path and a carried boost), then
Drive (the same day: a phantom pair riding with the hull, its stretch a warp and its tilt a turn). Played in the
artifact, Drive was *"just a plain boost with pressing both triggers ... it doesn't even need the black hole"*. The
design call (Yash, 2026-10-27): *"keep the black hole pair ... a little bit of realism, a little bit of sci-fi ... if we
can aim a little bit well and place them in a good distance, then we get the max speed boost ... we can't keep spamming
them ... they fall back together in a limited time, maybe 0.5 to 1.5 seconds."* Both earlier cuts are removed.

**Gate mode** (`StoatGateMath`, the gate in `StoatSlingExecutor`, the warp in `StoatPathfinderExecutor`; the studio's
`layGate` / `gateHold` / `gateJumped` / `stepGateBoost`, line for line):

| | |
|---|---|
| **Press** LT or RT | a REAL pair opens along the nose: the black hole `gateAhead` 90 u ahead, its white hole further along the same line — held, no pull on its owner, the Time-mode poles' light strength, a `gateMouthRadius` 5 u horizon. One gate at a time: none until the last has gone (no spam) |
| **Squeeze** | the deeper trigger stretches it: the white hole slides out, `gateLengthMin` 120 → `gateLengthMax` 600 u through the squeeze curve (`BlackHoleRegistry.Reshape`) |
| **Fly into the black hole** | the platform carries the hull through (`BlackHoleVesselPull.TryCarryThrough`, heading kept): out of the white hole, the gate's length skipped. Within `gateFunnelRadius` 30 u, still ahead, the black hole draws its owner sideways onto the line into its mouth at `gateFunnelRate` 4 /s — aim near it and it takes you in |
| **The jump's warp** | quality = aim × length: all of it for an entry straight down the gate's line, none at `gateAimConeDegrees` 25° off; × 0.4 for the shortest gate → 1 for the longest. The flight clock warps to `1 + (Boost − 1) × gateWarpShare 1.5 × quality` (×4 at rest for a perfect jump) for `gateBoostSeconds` 2.5 s, then fades |
| **Held / let go** | held, it lasts at most `gateHoldMaxSeconds` 3 s; let go (or flown through) it falls together in about `gateCollapseSeconds` 0.8 s (`BlackHoleRegistry.LetGo(pair, speed)`) and annihilates — throwing the Charge ripple |

**What is drawn:** the lensed holes themselves; a lime ring round the open gate's mouth (where to fly in); the path ahead
(lime while the warp is past half). The HUD's Time card is the warp's share; the Mass card half full with the Gate icon.
Keys and touch ramp a held trigger to a full squeeze (the longest gate) over `gateKeyRampSeconds` 0.5 s.

**Studio scorecard** (the AI at Hard, seed 1, race seconds; Mass / Time / Gate): circuit I1 54.0 / 60.4 / **53.5**,
I2 59.6 / 53.8 / **53.0**, I3 **51.5** / 77.2 / 54.8, I4 **49.2** / 79.5 / 57.7; hairpins I1-I4 **31-39** / 66-95 / 61-71.
The gate is a long-leg tool: the AI opens one only on a leg of `autopilotGateMinDistance` 300 u+ within
`autopilotGateConeDegrees` 12° of its nose, so the hairpins (no such legs) fly it unassisted. An AI that steers at the
mouth itself did 44.8 s on circuit I2, but the game's AI cannot be pointed at a mouth without overriding its mode's race
line, so both run the same rule and the funnel takes them in. A person who aims does better than the AI.

**The autopilot** switches into Gate for a long leg (`autopilotGateDistance` 400 u, within 30°) and out under half or past
twice the cone, presses RT with the squeeze that ends the gate 80 u short of its target (`StoatGateMath.AutopilotLength`),
holds while the gate is open and lets go once through or once it closes.

**Known limits.** The squeeze is not replicated: a peer's copy of the gate keeps a length of its own. The jump is the
platform's point-in-horizon test each frame: a very fast, off-centre pass (×4 warp past 100 u/s) can step over a 5 u
mouth; the funnel centres the approach. **Not done:** `gateModeEvent` ships empty; the icon is a placeholder; not yet
flown in the Unity Editor. The studio's line is 600 u and Unity's 1200 u (the existing `pathLength` drift).

## 1g. Round 19 (studio only) — the wormhole slingshot (2026-10-27)

**The team's call** (relayed by Yash, 2026-10-27): the pair should not teleport the vessel, it should **slingshot** it —
extra boost and a trajectory. Between the two holes is a wormhole: space bends **inward** at the black hole and
**outward** at the white hole. **LT** sets the distance between the two mouths, **RT** the throat's width. A thick
wormhole swings you in and out: a turn (70–90° and more); a thin one is a long straight boost for distance. *"Create a
wireframe to show me this geometry ... pressing the left trigger, it's making it more shorter and wider."*

**Built in the Vessel Studio first** (the artifact leads; Unity still runs §1f's straight gate until this is decided and
cloned with `/artifact-to-unity`). The studio's `whShape` / `whAt` / `gateHold` / `gateEnter` / `gateTransit` /
`gateOut` / `updateWormholes`:

| | |
|---|---|
| **Press** LT or RT | the black hole opens `gtAheadSeconds` 1.2 s of flight ahead (at most `gtAhead` 90 u); a wormhole runs from it to its white hole. The other trigger shapes the same one. One at a time |
| **LT** | mouths apart: untouched `gtLenMax` 600 u → fully squeezed `gtLenMin` 120 u, and the throat widens by `gtLtWiden` 0.5 of RT's range — LT alone makes it short and wide |
| **RT** | the throat's radius: `gtThroatMin` 3 → `gtThroatMax` 40 u. The mouths are `gtMouthFlare` 2 × the throat + 6 u (a hyperboloid: wide at the mouths, narrowest mid-way) |
| **Stick** | while shaping, the stick picks which way it bends (the hull holds its line into the mouth; a quarter of the stick's travel bends it fully). How far: `gtTurnMax` 120° × fatness, fatness = `gtTurnLenShare` 0.5 × LT's shortness + 0.5 × the throat. **LT alone: 90°; RT alone: 60° over a long arc; both: 120°**; neutral stick: straight |
| **Fly into the black hole** | crossing its mouth starts the ride: the throat carries you round its curve at `gtTubeSpeed` 2.5 × your speed, `gtThroatRush` +100 % at the throat; where you crossed the mouth is kept, scaled with the throat (you dive in toward it and swing back out) |
| **Out of the white hole** | your heading is the curve's last; the flight clock warps at once to `1 + (Boost − 1) × gtWarpShare 1.5 × q`, q = 1 − `gtFatCut` 0.65 × fatness (thin and long ×4, LT alone ×2.5, the fattest ×2), held `gtBoostSeconds` 2.5 s |
| **Held / let go** | held it lasts `gtHoldMax` 3 s; let go (or ridden through) both mouths fall together along the curve to its middle in `gtCollapse` 0.8 s and annihilate (measured: gone 1.5 s after a release, the annihilation included) |

**The wireframe:** rings round the throat and lines along it, violet at the black hole shading to white at the white
hole; at each mouth the sheet of space it opens from, bending in round the black hole and out round the white hole; in
lime the line you will fly, on along the white hole's heading. The mouths lens as if never nearer than 80 u, so the
mouth you fly at stays readable. The HUD reads the shape live: length, throat, the turn and the most this shape can turn.

**Measured** (headless, touch at half throttle): LT full + stick: in at 0.35 s, out at 0.87 s, **turned 90°**, ×2.5;
RT full + stick: 60°, ×3.0, 722 u covered; both full + stick: **120°**, ×2.0; LT and RT half, stick left: 53°, ×3.1.

**The AI** (input only): with its ring past `gtAiMinTurn` 120 u it tries 9 lengths × 13 bends, keeps the one whose white
hole leaves heading within 15° of the ring with 60 u to spare (thinner preferred: they warp harder), and squeezes the
triggers and holds the stick that make it; a straight one only for rings past `gtAiMin` 300 u. A rookie squeezes the
throat off the mark. **Scorecard** (Hard, seed 1; Mass / Time / Gate round 19, s): circuit I1 54.0 / 60.4 / **50.0**,
I2 59.6 / 53.8 / **49.6**, I3 51.5 / 77.2 / **49.1**, I4 **49.2** / 79.5 / 62.4; hairpins I1–I4 **31–39** / 66–95 /
44–53. Round 18's gate did 53.0 on circuit I2 and 61–71 on the hairpins: the bend made Gate the turning tool the team
asked for, and Mass keeps the tight hairpins.

**To decide before Unity:** the shape (these numbers), and whether the stick bending the wormhole (rather than turning the
hull) feels right while shaping. **Not modelled:** the network (the shape would have to replicate: two triggers and a
stick direction); a sound.

## 1h. Round 20 (studio only) — the tunnel reticle (2026-10-27)

**The team's call** (relayed by Yash, 2026-10-27, after round 19 *"was a hit, everybody liked it"*): the wireframe should
be a **reticle**. Holding a trigger shows the tunnel without making it; the player shapes it with LT and the sticks; letting
go of the triggers makes it in front of the hull and the hull goes in. *"This way I will have more control and time to
create my tunnel more precisely, even in such fast movement."*

| | |
|---|---|
| **Hold LT or RT** | the reticle: the tunnel as a cyan wireframe riding `gtReticleAhead` 30 u ahead of the nose, re-shaped every frame. Nothing is laid, no timer runs out. The shape rules are round 19's (§1g): LT shortens (and widens a little), RT widens the throat, the stick bends it while the hull holds its line |
| **Let go of both** | `gateCommit`: a real black hole and white hole at the reticle's mouths and the hull in it at once (it dives the 30 u straight to the mouth, then the throat carries it round). A tunnel still falling together behind you is ended by the new one |
| **Out of the white hole** | as round 19: the warp at once, most for a thin one; ridden through, it falls together in `gtCollapse` 0.8 s |

Gone with round 19's flow: the mouth opening ahead of you to fly into (`gtAheadSeconds`, `gtAhead`), its funnel
(`gtFunnel`, `gtFunnelRate`), the smallest mouth (`gtMouth`) and the hold limit (`gtHoldMax`). Their sliders are
removed; the values stay in the studio's numbers because the game's round-18 gate still reads them.

**The AI** plans as in round 19 from where the tunnel will start (`gtReticleAhead` + its speed × `gtAiAim`), holds the
reticle with the plan's triggers and stick for `gtAiAim` 0.3 s, and lets go. **Scorecard** (Hard, seed 1; Mass / Time /
Gate round 20, s): circuit I1 54.0 / 60.4 / **49.5**, I2 59.6 / 53.8 / **48.2**, I3 51.5 / 77.2 / **49.0**, I4 **49.2** /
79.5 / 53.0; hairpins I1–I4 **31–39** / 66–95 / 43–50 (round 19: circuit I4 62.4, hairpins 44–53).

## 1i. Round 21 (studio only) — the team's reticle controls and a confirm (2026-10-27)

**The problem Yash hit playing round 20:** letting go of both triggers made a straight 600 u tunnel, never the bent one
shaped in the reticle. Analog triggers pass back through zero over a few frames on the way out, and round 20 made the
tunnel from the last of them: no squeeze, no bend. *"We need a mechanic to confirm the tunnel ... with the right trigger we
control the right turn, with the left trigger the left turn, and like a double thumb the average vector ... in 4
directions."*

| | |
|---|---|
| **Hold the triggers** | the reticle, as round 20 (30 u ahead, nothing made, no time limit) |
| **RT / LT** | bend right / left: the bend's sideways part is RT − LT |
| **Both sticks** | their average bends it any of four ways (added to the triggers' part), while the hull holds its line |
| **How far** | `gtTurnMax` 120° at full; the further it bends, the shorter (`gtLenMax` 600 → `gtLenMin` 120 u) and fatter (throat 3 → 40 u). Squeezing **both** triggers harder also shortens it (both full: a 120 u straight hop) |
| **Make it** | **A** (Enter; **A · Go** on a phone) makes exactly what the reticle shows; the triggers must come up before the next reticle. **Letting go of both triggers** also makes it, from the shape held `gtReleaseGrace` 0.15 s before, so the triggers and sticks springing back never straighten it |

Round 19/20's LT-shortens / RT-widens shape rules (`gtLtWiden`, `gtTurnLenShare`) are gone.

**Measured** (headless, a 0.1 s release with the triggers and sticks springing back): RT full → 120° right, LT full →
120° left, RT 0.6 + LT 0.2 → 44° right over 408 u, both half + sticks up → 120° up, both full → a 120 u straight hop;
A with RT half → 57°, and still holding the triggers after A opens no second reticle. The made tunnel matched the
reticle every time.

**The AI** tries 21 bends × 9 squeezes, holds both triggers evenly and pushes both sticks toward the ring.
**Scorecard** (Hard, seed 1; Mass / Time / Gate, s): circuit I1 54.0 / 60.4 / **38.6**, I2 59.6 / 53.8 / **40.6**, I3
51.5 / 77.2 / **43.0**, I4 49.2 / 79.5 / **42.5**; hairpins I1–I4 **31–39** / 66–95 / 42–48. Gate is now the AI's
fastest mode on every circuit, by 10+ s: **a balance question** for the team (the warp after a thin tunnel, `gtWarpShare`
and `gtFatCut`, and the ride speed `gtTubeSpeed` are the levers).

## 1j. Round 22 (studio) — the config is the game's blueprint; the wormhole's look (2026-10-27)

**The team's call** (relayed by Yash): the right-side panel had duplicates (Field trajectory in Scene Config AND a "field
trajectory" Mode card in Play Style Config; the Time mode's "Dipole sling" section showing in Mass mode). Clean it into
one organized config whose parameters and names ARE what the game gets: *"this is just the blueprint of what's gonna be
made in the actual game ... we won't make a parameter name here something different in the game."* And the wormhole:
the grid is the player's own UI only; it must run exactly mouth to mouth, its mouths as wide as the holes; everyone else
sees only bent space-time, and how much concentrated black/white hole shows must be a setting.

**The six tabs now** (each section in exactly one; flight unchanged, the scorecard identical):

| Tab | Sections |
|---|---|
| Scene Config | Scene · Flight & course · **Black hole · look** · **White hole · look** · **Wormhole · look** · Birth & annihilation |
| Game Config | Intensity · Players |
| AI Config | Sim lab (now with the Gate AI rows) |
| Play Style Config | **Mode** (a dropdown: Mass / Time / Gate) · Play styles · Squeeze · **Pull & momentum** (Mass and Time) · then ONLY the mode in use: **Mass mode · the field trajectory**, **Time mode · the catch sling** or **Gate mode · the wormhole** |
| Input | Controls |
| Vessel Config | (one section at a time) Black hole · physics · White hole · physics · Pair life · Lope · Archive |

The hole presets (Pinpoint, Cinematic, Supernova, ...) moved onto the look cards; their Shipped button resets every row
a preset touches.

**The game's names.** Every slider shows its game name under it, from the page's `GAME_NAMES` (151 rows):
*linked* (green; `studio_to_unity.py` writes it: 73), *field* (the field exists in the game, not linked yet: check its
units when porting, e.g. `StoatSlingConfig.catchAhead` for the studio's Laid ahead), *new* (violet; the field the game
will get, by exactly this name, e.g. `StoatModeAction.gateTurnMaxDegrees`), *lab* (the studio's own, never in the game:
the play-style levels, the AI's sloppiness). The round-22 wormhole-look keys ARE their game names
(`gateDentStrength` ...). `studio_to_unity.py --check` now fails when a `linked` name disagrees with the map
(`--self-test` plants both failures).

**The wormhole's look** (`Wormhole · look`, StoatModeAction):

| | |
|---|---|
| The grid | yours only: your reticle, and your own tunnel while you ride it (`gateGridOnRide` 1). Rivals and AIs draw no grid. It runs mouth to mouth: the space sheets beyond the mouths are off (`gateGridSheets` 0) |
| The dent (everyone sees it) | each mouth of a made tunnel is a graded lens, `gateDentStrength` 0.6, as wide as the grid's mouth × `gateDentWidth` 1: space pulled in at the black hole's mouth, pushed out at the white hole's |
| The concentrated hole | `gateHoleCore` 0.12 × the mouth: a black shadow with its photon ring, and a white core; **0 = a pure warp of space-time**. The 3D hole spheres are no longer drawn on tunnels (the lens draws the core) |

## 1k. Round 23 (studio) — one thumb, all wormhole (Garrett, with Yash, 2026-10-27)

**Garrett's notes** (relayed by Yash, who asked that both designs converge): *"remove both the white hole and black hole.
lean completely into the wormhole. make this a one thumb flyer so the right stick is always free to shape the tunnel ...
continue to use the gridlines so the player can see the wormhole that will form when they pull the right trigger. once
the wormhole forms keep the gridlines but use the warping effects to convey the same tube to other players. only the
wormhole creating player should see the gridlines. everyone should see the visual warping that corresponds to the grid
lines."*

**How the two designs met** (Gate mode):

| | Yash (rounds 19–22) | Garrett (round 23) | Built |
|---|---|---|---|
| Flight | the dual-stick mix; the hull held its line while shaping | a one-thumb flyer | **the left stick flies** as the game's one-thumb hulls do (`SingleStickVesselTransformer`: Ease(2 × stick) yaw/pitch, a bank into the turn `gateBankShare` 0.12 × roll, full throttle); keys WASD |
| The bend | RT − LT plus both sticks, up to 120°, more bend = shorter and fatter | the right stick shapes the tunnel | **the right stick** bends it any way, up to `gtTurnMax` 120° (the arrows on keys); Yash's coupling kept |
| Length | both triggers harder = shorter | (not said) | **LT** shortens it (Yash's earlier "LT shortens"); LT full + stick centred = a 120 u straight hop |
| Seeing it | the reticle while the triggers are held | the grid always shows what RT would form | **the reticle is always on your nose** in Gate mode, faint at rest (`gateGridRestAlpha` 0.4), full while you shape |
| Making it | A, or letting go (with a 0.15 s grace) | pull the right trigger | **an RT press forms it** and you are in it (A, the release grace and the touch A · Go button are gone) |
| The holes | a concentrated core setting (round 22) | no black hole, no white hole | **gone**: no hole spheres, no cores, no light shells or gravitational wave when a tunnel forms or fades, no hole markers; the hole look cards hide in Gate mode |
| Others see | the mouths' dents | the warping that matches the grid | **the tube's warp** (`updateTubeWarp`): every live tunnel draws its tube, ring for ring the grid's surface, into a half-float buffer of screen offsets (the wall's view normal × sin(π·rim), in near the entry, out near the exit), which the lens pass adds to its bend; `gateTubeWarp` 0.8. The mouths keep their dents |
| Grid | yours only | only the creator | yours only: the reticle and your own tunnel while you ride it |

**Measured** (headless): the left stick full right turns right and banks right at full speed (58 u/s at cruise 60) with
the reticle up; the right stick full right → 120° right over 120 u, half right → 19° over 523 u (the ease makes the
middle of the stick fine), full up → 120° up, LT full → a 120 u straight hop, centred → 600 u straight; the formed
tunnel matched the reticle every time; RT again while riding does nothing; the reticle is back on exit.

**Scorecard** (the AI flies one-thumb too: the left stick for the ring, then holds its line `gtAiAim` 0.3 s while its
right stick and LT shape the plan, then pulls RT. Hard, seed 1, s): circuit I1–I4 **36.4 / 38.2 / 39.1 / 40.2**
(round 22: 38.6–43.0), hairpins 44.3 / 41.9 / 39.6 / 38.4 (42–48); Mass 31–39 on the hairpins. Full throttle made Gate
faster still: **the balance question for the team grows** (`gtWarpShare`, `gtFatCut`, `gtTubeSpeed` are the levers).

**Open, for Yash and Garrett together:** does "lean completely into the wormhole" also retire Mass and Time (both are
black-hole/white-hole modes, and the Stoat would become a wormhole-only, one-thumb vessel)? Round 23 changed only Gate
mode; Mass and Time are untouched. In the game this is also a control-scheme question: the Stoat's prefab runs the
two-stick `VesselTransformer`; a one-thumb Gate needs either a per-mode scheme or the Stoat moving to a one-thumb
transformer.

## 1l. Round 24 (studio) — inside a real wormhole: a smooth ride, a 3D grid, no false visuals (2026-10-27)

**Yash, playing round 23:** riding a tunnel, the camera "flipped 180 or 360" and jerked instead of going smoothly
through the tube; the centre of the tunnel should bend nothing and everything around it should be bent, like the
inside of a real wormhole; no black-hole or white-hole looks; make the grid 3D; save the hole looks for later.

| Problem | Cause (measured) | Fix |
|---|---|---|
| The view flipping on entry | a mouth's dent is a screen-space lens; as the camera reached the mouth its radius grew past the screen and the image inverted, then vanished when the mouth went behind the camera | the dent fades as the camera comes within `gateDentNear` 3 × the mouth radius of it; inside, the tube's own warp takes over |
| The camera jerking round the bend | the chase camera lerps its POSITION (k 7/s) and fell far behind a hull riding at 2.5 × speed, then swung round the curve | while you ride, the camera runs down the tube's **centreline** 18 u behind you (45 u in Follow) and looks 40 u ahead along it (`whAtX`: the straight run in and out included), eased in and out over ~0.1 s; through a 120° tunnel the view turns at most 5° a frame, its up at most 0.34° |
| A flat-looking grid | lines of one brightness read as a drawing | **3D grid**: a wall facing the eye is bright, the far wall 0.28 (`whFace`); a faint glass surface under your grid, lit at its silhouette (`gateGridGlass` 0.6) |
| Hole words on screen | the HUD still said "black hole + white hole · portal ON" in Gate mode | "one thumb · space-time bent into a tunnel" |

**Parked for later:** every black-hole / white-hole piece the tunnel used (the round-22 core, its shader lines, the hole
spheres, the light shells and gravitational wave, the markers) is listed with its commit in
`Docs/Studios/STOAT_PARKED_HOLE_LOOK.md`. Mass and Time keep their holes.

## 1m. Round 25 (studio) — out of a tunnel with the speed you went in with (2026-10-27)

**Yash:** *"you should come out ... with the same momentum and speed as you went inside the tunnel."*

- **`gateKeepMomentum` 1 (the default):** the speed you go in with (`vIn` = your engine, gravity and momentum speed × the
  warp you had) is the speed you come out with, on the tunnel's last heading. The warp you had going in is held,
  untouched, while you ride, and carries on after; nothing is added. 0 brings back rounds 19–24's slingshot boost
  (`gtWarpShare`, `gtFatCut`, `gtBoostSeconds`).
- **The ride:** it starts and ends at `vIn` and rushes to `gtTubeSpeed` 2.5 × it at the throat (sin along the tube), so
  there is no jolt at either mouth. `gtThroatRush` is folded into it and gone.

**Measured** (headless, a 120° tunnel at cruise): in at 59 u/s, 149 u/s at the throat, out at 60 u/s and steady 0.25–1 s
after; with `gateKeepMomentum` 0, out at 123 u/s (the old warp).

**What it does to the race** (the AI, Hard, seed 1, s): circuits I1–I4 103.0 / 100.6 / 98.8 / 97.6 and hairpins
85.9 / 79.5 / 75.0 / 71.7 - **Gate is now the slowest mode** (round 24: 36–40 and 38–44; Mass 49–60 and 31–39). A tunnel
now buys a turn at full speed and the rush through its throat, no speed after it. **For Yash and Garrett:** if Gate
should race, the levers that keep "out as you went in" are a faster ride (`gtTubeSpeed`), longer tunnels (`gtLenMax`), or
a faster one-thumb cruise; `gateKeepMomentum` 0 is the old boost.

## 1n. Round 26 (studio) — a tunnel's life: rippling in, staying, rippling out (2026-10-27)

**Yash:** the reticle should come back about 0.5 s after the exit and fade in like a wave emerging into the tunnel's
shape, reshapeable; on RT the tunnel forms the same way out of the centre of its geometry while the reticle fades; it
stays 10 s, then collapses the reverse way back to its centre, a ripple spreading out to infinity; every step smooth,
no camera jitter, real space-time warp visuals.

| Step | Built (`stepTunnel`, `gateHold`, `gateCommit`) |
|---|---|
| The reticle comes back | `gateReticleDelay` 0.5 s after you are out (and at the start of a race), then ripples out of its centre into the full tube over `gateReticleGrow` 0.8 s (ease-out; the window grows from the tube's middle to both mouths, its radius with it). You can reshape it as it grows; RT works once it is 60% there |
| RT: the tunnel forms | the warp - its mouths' dents, the tube's warp, your grid - ripples out of the same centre to both mouths over `gateFormSeconds` 0.4 s (never longer than 90% of your dive to its mouth, so the mouth is there when you are); the reticle fades out over the same time |
| The ride | unchanged (round 25): in and out at your entry speed, the camera on the centreline |
| It stays | `gateStaySeconds` 10 s after you are out (making another tunnel sends the last one back at once) |
| It collapses | back to its centre over `gtCollapse` 0.8 s, then fades, and a space-time ripple (the gravitational wave, `gateCollapseRipple` 0.4) spreads from its centre out to infinity |

**Measured** (headless): the reticle at 0 / 0.68 / 1 of its shape at 0.25 / 0.75 / 1.25 s; a 460 u tunnel growing
106 → 297 → 403 → 449 → 460 u over 0.4 s; the reticle absent for 0.5 s after the exit, then back over ~0.5 s; the tunnel
stayed 10.0 s, collapsed in 0.8 s, sent one ripple; the camera through the ride turned at most 2.4° a frame. The AI waits
for its reticle too: circuits 98.2-100.8 s, hairpins 72.5-88.2 s (round 25: 97.6-103.0, 71.7-85.9).

## 1o. Round 27 (studio) — save a section as a profile (2026-10-27)

Yash: *"I didnt find a way to save them in artifact to a new Wormhole Look settings, ... save my tweak settings to new
profiles of those areas like Wormhole Look, etc, for future re-play and design decisions."* Every settings card with a
Reset button (18 of them: Wormhole · look, Gate mode, Pull & momentum, the AI, the play styles, ...) now has a
**Profiles** row under its sliders: name the card's current values and **Save as profile**; pick one and **Load** it
(only that card's keys change; a key the profile lacks goes to its shipped value); **Record** writes it to the decision
log (topic *Profile*, with what it changes from shipped); **Delete** removes your own. "Now: <name>" shows which
profile the card matches. Profiles live in the studio's shared db (`profiles`, readable by Claude, everyone sees
everyone's) or, where the viewer has no db, in that browser. Nothing in the game changes: a profile is a set of studio
values; the one the team picks reaches Unity through `/artifact-to-unity` as usual.

## 1p. Round 28 (studio) — the wormhole becomes the Meerkat (2026-10-27)

Yash: *"a big change is this new wormhole ability ... This will be Meerkat, a new vessel. The Stoat will have that field
and orbit for now untouched. Just take away all this wormhole kind of ability of the Stoat and create a Meerkat ... keep
only one ability of the wormhole."* The studio's Gate mode (§1g-§1n) left the Stoat page and is now the Meerkat studio,
`Docs/Studios/VesselStudio/meerkat.html` (design: `Docs/Studios/MEERKAT.md`).

| What | Where it is now |
|---|---|
| Gate mode: the reticle, the tunnel, its ride and life, the tube's warp, the mouths' dents, the gate AI | The Meerkat studio only, with its parameters renamed to their future `MeerkatWormholeConfig` fields |
| B / T on the Stoat | Mass -> Time -> Mass (round 18-27 stepped through Gate too) |
| The Stoat's `gt*` / `gate*` keys, their sliders, tips and game names | Removed from the Stoat page |
| `studio_to_unity/stoat.json`'s 14 round-18 gate rows (`StoatModeAction` gate* fields) | Removed: the page no longer has those keys. The game's Stoat still has round 18's straight warp gate on B, unlinked from the studio until `/artifact-to-unity` ports the Meerkat and takes the gate off the Stoat |

Mass and Time are untouched: on seed 7, the AI at Hard raced them in 59.6 s (18 slings) and 53.8 s (17), the same as
before the split. Two small fixes came with it:
- `setStyle` had a `grp-P` visibility statement swallowed by a round-23 comment. It is a statement again, so Pull &
  momentum shows in Mass and Time only.
- The decision log tags new entries `studio: 'stoat'` and shows only the Stoat's, the older untagged ones included,
  because the Meerkat writes to the same shared collection.

## 1q. Time mode is the SKIM SLING (2026-10-29, first playable slice)

Yash's decisions (design plan: "Stoat Skim Sling: design plan"): the Skim Sling **replaces** Time mode's catch; it is
built straight in Unity (no Vessel Studio round first); rivals may **opt in** to riding it (`Docs/ELEMENTAL_ECONOMY.md`
§9b, flagged for Garrett, not wired). Names: the **Skimshell** (sphere of radius R round the pair's midpoint), the
**Skimline** (its equator, square to the spin axis), the **Tidepool** (the capture zone, `tidepoolScale` × R), a **Skip**
(one launch).

| Step | What happens | Where |
|---|---|---|
| Press | A light pair is laid `layAhead` ahead and `laySide` toward the pressed side, its axis the hull's up, poles at ±`poleHalfGap`·R. It spins the trigger's way (spin axis up for RT, down for LT), so the prisms' frame drag shows the spin | `StoatSlingExecutor.LaySkim` |
| Capture | Inside the Tidepool, as you pass (centre ≥ `catchDegrees` off the nose; inside R always). Entry angle θ against the surface sets the kept speed `lerp(plunkCarry, 1, cos²θ)`: flat keeps it all, straight in **plunks** | `StoatSkimMath.Captures / EntryCarry` |
| Ride | Pinned to R at `captureRate`; the nose eases onto the surface (`settleRate`) and follows a great circle (ω = v/R), drawn onto the Skimline at `precess`. The squeeze sets the spin `spinSpeedMax·s^spinExponent`; the ride's extra speed eases toward the spin's flow along your heading at `spinGrip` (with the spin gains, against it bleeds) | `StepSkim`, `StoatSkimMath.SpinFlow / SpinTarget / Precess` |
| Release | Launch along the surface at `min(capCruises × cruise, v_ride × (1 + G·Q'·rate·skipDecay^n))`, G = lerp(`launchGainMin`, `launchGainMax`, deepest squeeze), Q' = lerp(`missShare`, 1, the sweet spot on the angle ridden: `sweetSpotDegrees` ± `sweetSpotHalfWidth`, `sweetSpotFalloff`) | `LaunchSkim`, `StoatSkimMath.LaunchSpeed` |
| Hook | For `curveSeconds` after launch the course keeps turning about the spin axis at `curve` × spin/R, fading to 0 | `StepHook` |
| Skips | A capture within `skipWindow` of the last launch is the next skip of a run (n+1, to `maxSkips`); each pays `skipDecay^n` of the gain. Graze bounces and the perfect window are the next slice | `StoatSkimMath.SkipIndex` |
| Limits | `maxRideDegrees` ridden, or `holdMax` seconds held, launches you on your own; the let-go pair closes at `driftSpeed` and annihilates (the Charge ripple as before) | |
| Drawn | World dots only (no screen-space warp): a faint camera-facing **Tidepool** ring until captured, and the **Skimline** whose dots scroll with the spin, brighter once riding and lime in the sweet window. The catch warp is off in skim | `StoatPathfinderExecutor.AddSkimRings` |
| AI | Unchanged verbs: presses toward its turn, lets go when the target is in its cone (or wider in the sweet window), gives up an uncaptured shell after `aiCatchGiveUpSeconds` | `AutopilotSling` |

Config: `StoatSkimConfigSO` (`Assets/_SO_Assets/VesselActions/Stoat/StoatSkimConfig.asset`), referenced by
`StoatSlingConfigSO.skim`. Empty it to fall back to the catch (§1b). Tests: `StoatSkimTests`.

## 2. What the sink takes

| crosses the sink | what happens | where |
|---|---|---|
| **the owner** | carried out of the source (the boost line) | `StoatDipoleExecutor.FlyField`, `BlackHoleVesselPull.TryCarryThrough` |
| **a rival vessel** | carried out of the source, **stripped of its crystals** — every element's takeable level (`crystalStripShare` 1), left as crystals on the sink's side | `BlackHoleCrystalStrip.Levy` from `TeleportContinuity` |
| **a teammate** | carried through, not stripped | — |
| **a prism** | **vanishes** (destroyed at the horizon, scored to the owner, no debris — it fell behind the shadow); with **Space 5 (Theft)** it is **stolen**: carried out of the source in the owner's domain | `BlackHoleGravityField.ApplyVerdicts`, `Prism.Vanish` / `Prism.Steal` |
| **a falling prism meets another on the way in** | **both are destroyed**, scored to the owner — prism movement's collision rule (`Docs/PRISM_MOTION.md`); prisms that fall in already touching (a trail's neighbours) are at rest and do not. While the field holds ≤ 64 bodies they come apart as a high-res collision burst (`PRISM_ANIMATION.md` §4.12), otherwise the ordinary explosion | `BlackHoleGravityField.ApplyContacts`, `PrismMotion` |
| **a creature** | **swallowed**: killed within 1.5 sink horizons, its body suctioned into the sink (`Fauna.Predated(name, sink)`), on the fauna's simulating machine | `StoatDipoleExecutor.SwallowFauna` |

Nothing outside the pair is MOVED by it except by its own flying: an owned horizon pair pulls no
vessel (`BlackHoleVesselPull` skips it — a vessel may not move an opposing one,
`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED), so the owner's field flight is flown by the executor and a
rival only meets the pair by flying into it. Prisms are pulled by both poles (the black hole's own
field job).

**Colour.** The sink's shadow is drawn toward the owner domain's **dark** colour and the source's core
toward its **light** one (`BlackHole.DomainTint`, `_BHTint` in `BlackHoleLens.shader`, amount 0.7) — the
palette's `OutsideBlockColor` / `InsideBlockColor`, the prism faces' own pair.

**Ecology (restated per `/ecology` §2).** The swallow goes through the platform's predation path, so
the death is the platform's (heart released or stashed by `Fauna.Die`, the body suctioned, not popped —
continuity of existence holds) and it is a predation by a player, not an imposed death. A prism's
vanish happens AT the horizon, inside the shadow the lens draws, so nothing is seen to pop.

## 3. Network

The simulating machine (`MantaStingActionExecutor.IsSimAuthority`: the owner, the host for an AI)
reads the triggers, places the poles and publishes them on `R_VesselActionHandler.NetStoatDipoleSink`
(xyz + horizon) / `NetStoatDipoleSource` (xyz + the pair's serial in w: a press can close one pair and
open the next inside one tick, so a peer re-lays its copy when the serial changes instead of sliding the
old pair across) — the `NetEchoSightShape` precedent: the separation is two
analog triggers no peer receives, and the size is Space-scaled on an unreplicated level. Every peer
lays its own copy of the pair from those and nothing else, and applies the owner's rules to it (the
upgrade bit, the name and the domain are replicated). The boost and the field flight run where the hull
flies. Known limits, the platform's own: the gravity field's prisms and the stripped crystals are
local per peer (the wormhole toll's stance), so two peers can disagree on a prism at the horizon's edge.

**The hole budget is 16 (2026-10-12; it was FOUR).** At four, only two Stoats could hold a pair at once, and in lava lamp
or Slingshot with two AIs holding theirs the player's squeeze was silently refused ("sometimes my pair spawns, sometimes it
doesn't"). `BlackHoleConfig.maxBlackHoles` is now 16 (`BlackHolePhysics.NativeWells.Capacity`); the prism tide shader
draws the 4 holes nearest the camera and the lens the nearest 8. A squeeze refused for room keeps asking while the trigger
is held, so it opens as soon as there is room. A hole that is annihilating has already left the count.

## 3a. Ported from the studio: the settings map, the camera, the AI hand-over (2026-10-10)

**Settings.** Every field-dipole row of the studio is in `StoatDipoleConfig.asset`, authored by
`author_stoat_assets.py` (never by hand; `--check` fails a hand edit):

| studio | Unity | value |
|---|---|---|
| `ftAhead`, `ftSepMax`, `ftSepLong`, `ftSepFollow` | `aheadDistance`, `sidewaysMax`, `lengthwaysMax`, `followRate` | 250, 200, 120, 6 |
| `ftStrength` × 20,000, `ftHorizon`, `ftWhite` | `poleGM`, `poleHorizon`, `sourcePush` | 120 000, 3.5, 1 |
| `dpAccelCap`, `dpTurnCap`, `ftGrip` | `accelerationCap`, `turnCap`, `grip` | 3000, 12, 0.5 |
| `ftLength`, `ftStep`, `ftNose`, `ftMargin`, `ftMinLoop`, `ftWarpDeg`, `ftDotPx`, `ftDotGap` | `pathLength` … `dotGap` | 600, 3, 8, 8, 60, 3, 4, 10 |
| `ftBoost`, `ftRise`, `ftFade` | `boost` (Time: 2 → **3 at level 5** → 4), `boostRise`, `boostFadeSeconds` | 3, 20, 0.6 |
| `ftTurnScale` | the prefab's `PitchScaler` / `YawScaler` / `RollScaler` × 0.4 | 48 / 48 / 52 |
| `ftSteer` 1, `ftTrail` 0 | the pathfinder flies the held steering and never tests laid trail | — |
| `aiWarpQ`, `aiNear`, `aiLimeWait` | `autopilotHold01`, `autopilotLetGoNear`, `autopilotDrySeconds` | **1** (was 0.8), 60, 0.5 |

**Camera.** The Stoat flies on its own `StoatCameraSettingsSO` (stage 5 of the generator): the studio's chase
camera, 6.5 u up and 21 u behind (`followOffset (0, 6.5, -21)`), looking 40 u past the nose and 3 u up
(`lookAheadDistance` / `lookAheadLift`, `CustomCameraController.LookPoint`), easing onto both at 7/s like the
studio's (`chaseEaseRate`), framed for the studio's 68° (`framingFieldOfView`): the field of view stays the
player's setting (90 by default), and the camera moves nearer (×0.67 at 90) and turns its look so the hull reads
the same size and sits at the same height on screen as in the studio. Until 2026-10-10 it carried the
Squirrel's camera (flat, 17 u straight behind, aimed at the hull).

**The pair's look is the studio's.** `domainTintAmount` ships at **0**: the sink's shadow is black (at 0.7 it
read as the owner's dark JADE) and the source's core white-hot. The lens draws the studio's warm photon ring at
the shadow's edge (`BlackHoleConfig.photonRingGlow` 0.55, `photonRingWidth` 0.06).

**The AI hands the hull back clean.** The lava lamp flies the menu Stoat on AI; its path-watching autopilot
lays pairs there. When you click to fly, `StoatDipoleExecutor.HandBackAiHold` lets go of whatever the AI was
pressing (through the replicated stop), so the AI's pair closes. Before it the press stayed held forever:
the pair laid in the menu stayed open and its field flung your hull from the first frame of freestyle,
which read as "a black hole spawned in the centre, the camera off the vessel".

## 3b. The pair as the studio draws it, the key squeeze, the 3D path (2026-10-10)

**Both poles always show.** The lens is the studio's own (`Docs/BLACK_HOLE.md` §5.1, 2026-10-10): every hole is drawn in
ONE screen pass that sums their bends and samples the scene once, so neither pole can paint over the other, and nothing
is swapped for the skybox, so no disc shows round a hole in lava lamp. (Each hole used to draw its own 30-horizon lens
sphere; the poles sit 60-200 u apart, so the spheres overlapped and the one drawn last erased its partner.) Where the two
holes overlap on screen, the black shadow is drawn over the white core, exactly as in the studio.

**Where the poles go is the studio's**, checked line for line: laid `aheadDistance` ahead in the frame of the press,
sideways = the triggers' difference (the black hole on the deeper side), lengthways = their sum (the black hole
nearer), mirrored about the laid middle. What differed was a KEY: the studio squeezes a key to 0.5 at once
(`dpKeySqueeze`), Unity ramped it 0 → 1 over 1.5 s, so on keys the poles sat on top of each other first and then
opened wider than the studio's (200 u, not 70). `keySqueeze` 0.5 is the studio's. A phone's held button is a key too (2026-10-21,
`STOAT.md` §4a): no trigger depth on glass, so a touch pair opens at `keySqueeze` the same way.

**The path is in the scene.** `StoatPathfinderWorldDots`: camera-facing discs every `worldDotSpacing` (3 u: denser
than the studio's 2D line) along the 3D path, `worldDotSize` 0.7 u, never under `worldDotMinPixels` 2.5 px. They
recede, pass behind prisms and bend through the lens. `dotsInWorld` off brings back the studio's flat screen dots (the
path only; Time mode's rings are world dots).

**Fixed 2026-10-14 — the path was never seen ("I no longer see the foresight").** The first cut drew the dots as ONE
INSTANCED draw on the built-in `Sprites/Default`. That shader's instanced variant reads the sprite renderer's flip and
colour from per-instance arrays (`unity_SpriteFlipArray`, `unity_SpriteRendererColorArray`) that
`Graphics.RenderMeshInstanced` never fills: both read zero, `UnityFlipSprite` scaled every quad to a point, and the line
vanished the day it replaced the screen overlay. The dots are now ONE dynamic mesh a frame (each dot a quad billboarded
on the CPU, its colour in the vertex colour), drawn as a single identity instance (`Graphics.RenderMeshInstanced`, the
call Amoebius's shim also has) with the project's own `CosmicShore/StoatPathDots` shader (`Resources/StoatPathDots.mat`,
instancing on, so builds carry it; its only per-instance datum is the matrix the call fills). `Begin` / `Add` / `End`
lets the path and Time mode's two rings share the one draw.

**Tune it in the artifact, then clone it:** the Vessel Studio artifact's Stoat page is the one source of truth for these
numbers; `/artifact-to-unity` writes them into `StoatDipoleConfig` and `BlackHoleConfig` (`Tools/Build/studio_to_unity.py`,
map `Tools/Build/studio_to_unity/stoat.json`).

## 4. Files

| | |
|---|---|
| Maths (pure, tested) | `StoatDipoleMath.cs` — `StoatDipoleTests`; `StoatSlingMath.cs` (+ `SweetSpot`, `TimedKick`, `CatchPair`, `Catches`, `RingNormal`, `Ring`, `SettleHeading`) and `StoatRippleMath.cs` — `StoatRippleTests`, `StoatSlingTests` |
| Placement + field flight + owner rules + Accretion + AI | `Executors/StoatDipoleExecutor.cs` |
| Time mode's catch + orbit + timed slingshot + AI | `Executors/StoatSlingExecutor.cs` |
| The mode switch + thick trail + AI switching | `Executors/StoatModeExecutor.cs`, `Data Containers/StoatModeActionSO.cs` → `StoatModeAction.asset` |
| The ripple | `Executors/StoatRippleExecutor.cs`, `StoatRipple.cs`, `Data Containers/StoatRippleConfigSO.cs` → `StoatRippleConfig.asset`, `_Graphics/Materials/Graphs/StoatRipple.shader` (`Tools/Shaders/verify_stoat_ripple.py`), `Resources/StoatRipple.mat` |
| Pathfinder + boost + dots + Time mode's rings + Foresight + Overclock | `Executors/StoatPathfinderExecutor.cs`, `StoatPathfinderWorldDots.cs` (the path and rings in the scene) + `_Graphics/Materials/Graphs/StoatPathDots.shader` / `Resources/StoatPathDots.mat`, `StoatPathfinderDots.cs` (screen overlay, after the lens), `ImpactEffects/…/StoatOverclockByCrystalEffectSO.cs` |
| Config | `Data Containers/StoatDipoleConfigSO.cs` → `_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset` (+ Accretion, Foresight), `StoatSlingConfig.asset` (Time mode: the catch, the light poles, the rings, the timing window) |
| Triggers | `StoatDipoleLeft/RightAction` + `StoatSlingLeft/RightAction` on each trigger; `StoatHoldAction` on X; `StoatModeAction` on B |
| HUD | `UI/Controller/StoatHUDController.cs`, `UI/View/StoatHUDView.cs`, `_Prefabs/UI Elements/VesselHUD/StoatHUDVariant.prefab`, `_Graphics/Icons/AbilityIcons/Stoat/` |
| Own assets | `StoatCameraSettingsSO` (`_SO_Assets/Camera/StoatCameraSettingsSO.asset`, stage 5), `StoatImpactorDataContainer`, `StoatSkimmerImpactorDataContainer`, `StoatAIBoostPolicy`, `CardImages/Stoat(_Inactive).png` |
| Platform hooks | `VesselTransformer.FlightTimeScale`, `VesselPrismController.ThicknessMultiplier`, `BlackHole.PrismCapture` / `CrystalStripShare` / `DomainTint` / `CapturedCount`, `BlackHoleRegistry.PairMet`, `Prism.Vanish`, `BlackHoleCrystalStrip`, `R_VesselActionHandler.NetStoatDipole*` |
| Prefab + assets | `Tools/Build/author_stoat_assets.py` stages 4, 5 and 6 (`--check`, `--self-test`) |
| Arcade | `Arcade/WARPLINE.md` (the Time race), `Arcade/SLINGSHOT.md` (flown on the dipole; the AI uses both modes) |

Audio slots ship **empty**: `openEvent`, `annihilateEvent`, `warpEvent` (dipole config), `holdStartEvent`, `slingEvent`,
`sweetSpotEvent` (sling config), `massModeEvent`, `timeModeEvent` (mode action), `rippleEvent`, `stripEvent` (ripple config).

## 5. Element map

| Element | Ability | Input | Scales | L5 upgrade (gate) |
|---|---|---|---|---|
| **Space** | Field Dipole | LT / RT (Mass mode) | pole size ×1 → ×2; orbit radius ×1 → ×1.4 in Time mode | **Accretion** (+ Theft) — `StoatDipoleExecutor` `IsUpgradeActive(Space)` |
| **Time** | Pathfinder / Time Mode | passive | warp boost ×2.5 → ×5; the timed kick by its ratio | **Foresight** — `StoatPathfinderExecutor` `IsUpgradeActive(Time)` |
| **Charge** | Ripple | passive | the ripple's reach ×1 → ×2 | **Siphon** — `StoatRippleConfigSO.Siphons` `IsUpgradeActive(Charge)` |
| **Mass** | Mass / Time Mode | B | Mass-mode trail thickness ×3 → ×6 | **Armoured Wake** — `StoatModeActionSO.ShieldsInMassMode` `IsUpgradeActive(Mass)` |

`python3 Tools/Build/element_ability_table.py Stoat` reads 4/4 · 4/4 · 4/4, 0 disagreements. `halfGapHorizons` (Space)
is read only by the crystal pair style (`BlackHoleConfig.crystalPairs`), off by default. The forked containers carry
none of the Squirrel's element gates (rule 26): its omni explosion / crystal morph / Crystal Joust / steal / overtake /
boost-meter skim are gone, so no element grants a second, undocumented upgrade.

## 6. Proposals (as written 2026-10-09 — DECIDED 2026-10-13, see `FLEET_MAPS.md` ▸ Stoat for what was chosen)

Every row below is a proposal awaiting sign-off, per the vessel skill's design gate. Each keeps the
fleet's element conventions (Space = reach, Time = rate, Charge = threat, Mass = size) and one
parameter per element, and each is something the existing pair can do rather than a second system.

### Mass ability

| | Proposal | Mass scales | Why |
|---|---|---|---|
| **M1 (recommended)** | **Accretion** (passive). What the sink swallows FEEDS the pair: each swallowed prism's volume grows both poles' horizon and GM, held until the pair annihilates. | the growth per unit of swallowed volume (×0 → ×2) | Volume is the spine: the pair gets heavier by eating, so a pilot who feeds it in a prism field earns a bigger, wider-bending field. Emergent, no new verb; it uses the capture the sink already makes. |
| M2 | **Ballast** (on X, replacing the hold). Hold X and the hull is HEAVY: the field turns it less but its gravity speed builds faster. | the ballast factor | A pilot chooses bend or speed per pass. Costs the stop, which the studio never needed. |
| M3 | **Trail mass** (passive). Mass sizes the Stoat's trail prisms, the fleet's most common Mass channel. | trail prism size | Cheapest; the trail then feeds a rival's sink or your own (M1) more. |

### Charge ability

| | Proposal | Charge scales | Why |
|---|---|---|---|
| **C1 (recommended)** | **Annihilation blast** (passive). When the pair annihilates, it detonates at the meeting point: an area blast whose radius is the separation the poles had when you let go — the dipole moment becomes the threat. | blast radius / damage | Threat from the one act the ability already has (letting go). A pilot can collapse a wide pair on a rival or a prism wall. |
| C2 | **Throat shear** (while held). The segment sink → source cuts: prisms within a few units of the line between the poles are destroyed. | the cut's width | Turns the separation into a blade; reads clearly on screen. |

### Omnicrystal (what collecting an omni crystal does for a Stoat)

| | Proposal | Why |
|---|---|---|
| **O1 (recommended)** | **Overclock**: for 6 s the boost holds at full whether or not the path is warped (the line stays lime). | The Time race's fantasy, and a clean, readable reward. |
| O2 | **Lodestone**: for 8 s loose crystals within your sink's reach fall into it and come out of the source toward you. | Leans into the strip: what you take off a rival comes to you. |
| O3 | **Horizon ward**: 8 s of elemental-debuff immunity (`VesselTimedElementalWard`, the Sparrow's omni). | Reuses a shipped component; protects you diving through a rival's sink. |

### Missing level-5 upgrades

| Element | Proposal | Why |
|---|---|---|
| **Time 5 — Slipstream (recommended)** | The pathfinder counts EVERY hole as warp: a path bent by a rival's pair or an environmental black hole boosts you too. | Emergent: rivals' pairs become your road. Uses the field the lens already draws. |
| Time 5 — Foresight | The pathfinder also draws rivals' predicted paths in their domain colours. | Information, not speed; a strong racing read. |
| **Charge 5** (with C1) | **Pair production**: prisms the annihilation blast destroys come back as your domain at the blast's edge. | Mirrors Theft on Charge's axis. |
| **Mass 5** (with M1) | **Event horizon**: an accreted pair also swallows SHIELDED prisms (devastates them) and strips a rival who only grazes the horizon. | Mass's L5 makes the heavy hole heavier, not a new act. |

## 7. Status

Compiled headless against the Unity references (`Tools/Build/unity_refcompile`, player config: 0
errors in project code). **Not run in the Editor** — `/verify-unity` was not available in the session that
wrote it; filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Not yet measured in the game: the feel of
the field flight against the transformer's nose follow (the studio turns the hull directly; here the
field turns the hull and its command together, which keeps the follow's gap), and the per-frame cost
of the prediction near the sink (≤ 96 substeps per 3 u step, ~400 steps since the path went to 1200 u on
2026-10-13 — profile `StoatPathfinderExecutor.Update` before anything else).

Since the port (`cece/fervent-hypatia-rg4why`, 2026-10-13), four rounds of play tuning landed here
(§1 tells each number's history). The pure maths behind each of them was RUN headless: the shipped
`StoatDipoleMath` on a stub `UnityEngine` (`/asset-surgery` §4), the fly-through and sensitivity scenarios,
and the new `StoatDipoleTests`. The lens change passed `verify_black_hole_lens.py`. The whole tree compiled
against the Unity references, and none of it has been opened in the Editor.

**The own-vessel pass (2026-10-13, this branch)** added §1a–§1e and stage 6 of the generator. Verified headless:
`unity_refcompile` player + editor configs (0 errors in project code), `author_stoat_assets.py --check` / `--self-test`
(22 negative controls; stages 2–6 idempotent; no dangling local reference after the surgery), the peer-press harness
(1050 human + 1050 AI presses, 0 divergences, with both trigger assets on each trigger and B bound),
`author_stoat_icon_placeholders.py --check`, `vessel_model/render_cards.py Stoat --check`, `element_ability_table.py Stoat`
(4/4, 0 disagreements) and the repo's logging / conditional / abstract-member / switch / using / network-hash /
container / channel / far-clip / class-icon / elemental-float gates. `StoatRippleTests`' assertions were RUN headless (the shipped `StoatRippleMath` / `StoatSlingMath` on the hull
harness's math stub: 29 of 29 pass); the NUnit suites (`StoatRippleTests`, the edited `StoatSlingTests` /
`CrystalHullFusionBakeTests`) compile in the editor config but were not run in an editor. The
ripple shader compiles in both stages against the lens verifier's URP mock (`Tools/Shaders/verify_stoat_ripple.py`, with
a negative control); the real-URP DXC compile was not available. **Not opened in the Editor** — `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 8. Debt and follow-ups (rows only — the 2026-10-13 and 2026-10-14 ship passes)

| # | Kind | Row | Measured |
|---|---|---|---|
| D1 | **resolved 2026-10-13** | The round-1 **sling family** was left on the prefab unbound with the question "kept or retired?". Decided: **kept, as Time mode** (§1a–§1b). Its actions ride both triggers beside the dipole's; the two comments that named it as live (`SlingshotController`, `BlackHoleVesselPull`) are true again. | `grep -rln StoatSling Assets/_Scripts --include=*.cs` — every hit is live code. |
| D2 | debt walked past | **Two view-camera resolvers.** `BlackHoleLens.ViewCamera()` (the last base camera that rendered to screen) predates bleeding-edge's `CameraManager.ViewCamera` (`a315a308`, the active controller's camera), and both answer "which camera is the player looking through". The pathfinder tries the controller first and then falls back to the lens's resolver. Pick one; a third view-dependent effect will otherwise pick at random. | `grep -rn "BlackHoleLens.ViewCamera()"` → 10 call sites; `CameraManager.ViewCamera` → 8. |
| D3 | debt this branch created | `StoatDipoleMath.ShouldAnnihilate` now gates a path that cannot be reached in normal play: since 2026-10-13 a release closes the pair at once (`Release` → `Close(annihilate: true)`), so the "let go of both and the poles fall together" branch runs only if a pole is left open with no trigger held. Keep it as a safety net or delete it along with its test. | one caller, `StoatDipoleExecutor.StepAuthority`; its doc still says "Let go of both triggers and the poles fall back together". |
| D4 | **resolved 2026-10-13** | The ported **Crystal Wormhole Cell** was referenced by nothing outside its own folder: the port skipped the source branch's Cell Selector entry believing it named a deleted Black Hole cell, but that cell was RENAMED to the Crystal Wormhole Cell with its guid kept (`eb902588`, `R100`), so the entry named this very config. `Docs/CRYSTAL_WORMHOLE.md` and `CrystalWormholeTests` both say it is selectable; Menu_Main's `Cell.CellConfigs[13]` is restored byte-for-byte from the source branch (DT-003). | `grep -c dc10d5b4b02b4bd2a0890b2679ce32aa Assets/_Scenes/Menu_Main.unity` → 1. |
| D5 | report | The **Stoat Flight Studio** now describes a different vessel (labmaker CATALOG row, L-STU-29). The next studio round starts by re-reading `StoatDipoleConfig`. | — |
| D6 | debt this branch created | **The mode is not replicated state, only replicated presses.** Every peer flips on the replicated B press (the Butterfly's mode does the same), so a peer that JOINS after the owner switched starts its copy in Mass mode and routes that Stoat's trigger presses (and lays its trail) as Mass mode until the owner switches again. A `NetworkVariable<byte>` on `R_VesselActionHandler` beside `NetStoatDipoleSink` would fix it for both hulls. | late join only; the peer-press harness's 920 late-joiner releases are of presses, not of mode state |
| D7 | report | **The ripple bends what is in front of it.** `StoatRipple.shader` has no depth test (the lens has one, because the lens's sphere is centred on a hole the scene can be in front of); an opaque between the camera and the band is bent with the background for the wave's ~1 s. Add the lens's `SampleSceneDepth` test if it reads wrong in play — it needs the depth texture the lens switches on only while a hole lives. | — |
| D8 | report | **Four holes, two pairs** (§3) now covers BOTH modes: a Time-mode orbit pair counts against the same budget as a dipole. Unchanged in kind, wider in reach. | `BlackHoleConfig.maxBlackHoles` 4 |
| D9 | report | The Stoat's **hull-fusion entries ship with no bake** since its last model left the asset: the fusion solves on a worker at runtime (`CrystalHullFusion.Prewarm`, the Scarab's path onto its visible procedural hull), and `CrystalHullFusionBakeTests` skips a runtime-generated hull instead of demanding a bake keyed to a mesh the vessel never draws. | `RuntimeGeneratedHull` in the test |
| D10 | debt this branch widened | **The Stoat's sim-authority test lives on another vessel's executor.** Every Stoat executor asks `MantaStingActionExecutor.IsSimAuthority(status)` whether it is the simulating machine — a general rule (the owner; the host for an AI) homed on the Manta's sting. This branch added a fourth caller (`StoatModeExecutor`). Shape: a helper on the wrong class (`/refactor` §3). Move it to a vessel-level home (`IVesselStatus` / a static `VesselAuthority`) and repoint every caller. | `grep -rln "MantaStingActionExecutor.IsSimAuthority" Assets/_Scripts --include=*.cs` → 4 files, all four Stoat executors (the Manta's own uses are unqualified) |
| D11 | walked past | **`check_enum_member_references.py` is red on bleeding-edge**, not on this branch: `GameCanvasUnifier.cs(920): 'Placement' has no member 'Capture'`, the enum it resolved being `CrystalFlipWaveBenchmarkWindow.cs`'s — likely two same-named enums rather than a missing member. Confirmed identical on `390c2467` (exit 1) before any Stoat change. | `python3 Tools/Build/check_enum_member_references.py; echo $?` → 1 on base and branch |
| D12 | walked past, FIXED here | `Tools/Build/peer_press_harness` could not compile on bleeding-edge: its `UnityEngine` stub had no `Vector4`, which the Stoat port's `NetStoatDipoleSink/Source` added to `R_VesselActionHandler`. One stub line; the harness now runs (1050 + 1050 presses, 0 divergences). | `python3 Tools/Build/peer_press_harness/run.py --self-test` → was COMPILE FAILED, now passes |
| D13 | resolved 2026-10-28 (`stoat/annihilation-waves`): the death waves are on again, their light after post-processing; the birth wave is behind `birthWave`, off | **`BlackHoleWaves` had no caller by default.** Its only producer (`StoatDipoleExecutor`) now spawns it behind `StoatDipoleConfig.annihilationWaves`, OFF (2026-10-14), so the wave pipeline (`BlackHoleWaves`, the lens shader's wave loop, `PrismGravityWarp.hlsl`'s strain, ~11 `BlackHoleConfig` wave fields) runs for nobody, and `studio_to_unity/stoat.json` still clones the studio's wave keys into it. Decide: keep as an opt-in look (and say so on the studio page), or retire the pipeline with its studio rows. | `grep -rn "BlackHoleWaves.Spawn" Assets --include=*.cs` → 3 sites, all inside `if (config.AnnihilationWaves)` |
| D14 | walked past | **`Tools/Shaders/verify_stoat_ripple.py` FAILS on bleeding-edge** under glslang 15.1 (`lens_vert.hlsl:9: 'declaration' : Expected`, both stages, before any change here); its negative control still "fires", so the harness reports FAIL while looking like it runs. The lens verifier's URP mock likely needs updating for this glslang. | `git stash; python3 Tools/Shaders/verify_stoat_ripple.py` → FAIL on `b645b9d3` |
| D15 | report (port) | **Amoebius's shim lacks** `Graphics.RenderMesh`, `Mesh.Clear(bool)`, `Mesh.SetColors(Color[], int, int)`, `Mesh.SetIndices(int[], int, int, …)`, `RenderParams.camera` and `Vector3.RotateTowards`; this branch wrote around them (vessel skill rule 51). A port-branch row, not a game change. | `dotnet build Port/src/CosmicShore.Live` on the first cut of this branch → 6 errors, all of these |
| D16 | report | **`STOAT.md` §1–§2 describe the round-4 orbit**, which no longer ships (Time mode is the catch-sling, §1b). The banner says so; the sections stay as the orbit's record. Fold them into a short history paragraph when the catch-sling settles in play. | `STOAT.md` banner, 2026-10-14 |
