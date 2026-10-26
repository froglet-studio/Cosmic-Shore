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
**Mass → Time → Drive → Mass** since 2026-10-26 (`StoatModeActionSO` → `StoatModeExecutor.Toggle`; Drive is §1f).
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

## 1d. The HUD — four cards, four gauges

`StoatHUDVariant.prefab` (a true variant of `VesselHUDPrefab`, copied once from the Thresher's: authored petal bars, a
four-card row on known fileIDs), `StoatHUDView`, `StoatHUDController` (every signal polled from the Stoat's OWN
executors, serialized on the prefab — rule 14):

| Card | Icon | Gauge |
|---|---|---|
| Charge | Ripple | the ripple a let-go would throw NOW, as a share of the largest |
| Mass | Mass / Time Mode | the switch: full in Mass mode, empty in Time mode, eased between |
| Space | Field Dipole | Space-5 Accretion: how far the open pair has grown |
| Time | Pathfinder | Mass mode: the warp boost on; Time mode: progress to the 90° sweet spot, lime in the window (the warp runs in both); Drive: the warp pair's warp as a share of its top, ready past half |

The Mass card reads the mode: full in Mass, **half in Drive**, empty in Time, and its **icon follows the mode**
(2026-10-26, `StoatHUDView.SetModeIcon`): `Stoat_ModeMass` (the thick trail and its path), `Stoat_ModeTime` (an orbit
round a solid attractor), `Stoat_ModeDrive` (the warp pair: a black hole ahead, a white hole behind, the hull between), placeholders from
`author_stoat_icon_placeholders.py` (`--check`) wired on `StoatHUDVariant.prefab`; an empty slot shows the card's own icon.

**On a phone** (Android, iOS: `KitInputDevice.IsTouchPrimary`, and only while no pad is connected; never on PC or
Steam) the Stoat flies the TotemButtons profile (`author_touch_profiles.py`): the cards round the right thumb are its
buttons. **The Mass card is B** (its ability's Input 7, `Button2Action`, routed to `StoatModeAction` by the prefab's
`_touchActionOverrides`), so a tap steps Mass → Time → Drive and the icon shows where you are; the **Space card is LT**
(the left dipole, the left catch in Time, the left half of the warp pair in Drive), the **Dipole disc is RT** and the **Hold disc is A**.
`StoatSlingTests.Touch_TheModeCardIsTheModeSwitch_OnAPhoneToo` pins the binding. Open: the Dipole disc and the Space
card keep their dipole icons in Time and Drive mode.

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

## 1f. Drive mode — the warp pair (2026-10-26)

The design call (Yash), after a first cut (Hybrid: the orbit with a half-length path and a carried boost) was removed:
*"we will use the field trajectory as more precise movement and the orbit as more fast agile movement, but less accuracy
... can you make [a third mode] so that we can also have a bigger control of long distance travel with agile movement and
precision? ... determine this precision level or agility level with how much the black holes are differing from each
other, or the position of the black hole and the white hole pair, or the size of them ... it cannot be just the blend. It
needs to be recreated by inspiring the other two."* B's third press: **Mass → Time → Drive → Mass**.

**The idea: a warp drive.** In general relativity the Alcubierre drive contracts space ahead of a ship and expands it
behind. The Stoat already owns a black hole (a sink) and a white hole (a source); in Drive mode they ride WITH the hull,
the black hole ahead of the nose and the white hole behind it, mirrored through the hull, and the triggers shape the pair
the way they shape Mass mode's dipole (`StoatDriveMath`, ported line for line from the studio's `driveShape`):

| The triggers | The pair | What it does |
|---|---|---|
| **Sum** (both squeezed) | its LENGTH: `driveLengthMin` 60 u → `driveLengthMax` 320 u ahead and behind | the WARP: the flight clock to `1 + (Boost − 1) × driveWarpShare × span^driveWarpExponent` (×3 at rest at full stretch, Time scales it) — the long legs |
| **Difference** (one deeper) | the black hole slides to the deeper trigger's side, up to `driveLateralMax` 140 u, the white hole the other way | the pair TILTS (`atan2(sideways, length)`) and the tilt TURNS the hull toward the black hole: `driveTurnGain` 2.2 °/s per degree, up to `driveTurnMax` 140 °/s, on the warped clock |
| **The sticks** | — | turn on the REAL clock (`ExternalTurnRateMultiplier` = 1 / the warp): a long fast leg turns wide |

**So the pair's shape is the dial between agility and precision** — the user's "how much the holes differ, where they sit":
a short pair pulled sideways tilts a long way and **carves** (the orbit's agility); a long straight pair barely tilts and
**travels** fast and steady (the long legs); a light squeeze with a small difference **trims** (the field's precision —
the warp curve keeps a light squeeze near the hull's own speed). The same difference turns a short pair more than twice
as hard as a long one (`StoatSlingTests.Drive_TheSamePull_TiltsAShortPairMore_ThanALongOne`).

**What is drawn:** the pathfinder's line, flown with the drive's turn and on its warp (the field's precision tool: the
arc you see is the arc you fly), easing toward lime with the warp and lime past half of it; and a ring where each pole
rides (black hole violet, white hole white). The HUD's Time card is the warp's share of its top; the Mass card sits half
full and shows the Drive icon. Keys and touch ramp a held trigger to a full squeeze over `driveKeyRampSeconds` 0.5 s.

**It takes nothing and pulls nobody.** The pair is the hull's drive, not a pair in the world: no horizon, no prism taken,
no vessel pulled (PvP stays petals only), no lens pass (no frame cost). The trail is laid as authored.

**Why the sticks run on the real clock** (measured in the studio before porting). With the sticks on the warped clock —
the field's rule, where the warp is pure speed — Drive won every course by 30–40% and made both other modes pointless.
On the real clock, stretching for speed costs turning radius, and the trade-off appears (the AI at Hard, seed 1, race
seconds):

| Course | Mass (field) | Time (catch) | **Drive** |
|---|---|---|---|
| Circuit I1 / I2 / I3 / I4 | 54.0 / 59.7 / 51.5 / 49.2 | 60.7 / 56.1 / 75.8 / 81.7 | **45.8 / 45.4 / 43.8 / 41.8** |
| Hairpins I1 / I2 / I3 / I4 | 38.8 / 33.2 / 32.0 / **31.0** | 90.6 / 80.4 / 65.2 / 66.2 | **35.2** / **32.6** / 33.3 / 37.1 |

Drive wins every open circuit (the long legs), is level with Mass on the medium hairpins, and Mass wins the tightest
(the small manoeuvres) — the split the design asked for. The warp share was 1.5 in the first cut (Drive too strong); 1.

**The autopilot.** In Drive mode it squeezes its own triggers (`StoatDriveMath.AutopilotSqueeze`, the studio's AI):
stretch with the distance still to go (full from `autopilotDriveFar` 150 u), none while its target is more than
`autopilotDriveConeDegrees` 120° off the nose (turn first, then travel), and the difference toward the target, full at
`autopilotDriveLeanDegrees` 15° of yaw. It switches INTO Drive for a long leg — its target at least
`autopilotDriveDistance` 400 u away and within `autopilotDriveSwitchConeDegrees` 30° of the nose (0 = never) — and out
under half that distance or past twice the cone, two B presses round the cycle in one go when it must.

**Where it runs.** The mode replicates (B is a replicated press); the squeezes, the pair, the turn and the warp run on
the simulating machine (the pathfinder's own rule), and peers see the flight. The flight recorder writes `timeMode` 2.

**The studio** (`Docs/Studios/StoatFlightStudio.html`, round 17): `slingMode 'drive'`, the Drive mode card (`grp-V`, the
`dr*` rows, mapped to `StoatModeAction` by `studio_to_unity/stoat.json`, all twelve matching), T and the phone's B button
step the three modes. **Known difference:** the line's length is 600 u in the studio and 1200 u in Unity (the existing
`pathLength` drift `studio_to_unity.py --check` reports). **Not done:** the FMOD slot `driveModeEvent` ships empty; the
pole rings are dots, not lensed holes; the icon is a placeholder; not yet flown in the Unity Editor.

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
| D13 | debt this branch created | **`BlackHoleWaves` has no caller by default.** Its only producer (`StoatDipoleExecutor`) now spawns it behind `StoatDipoleConfig.annihilationWaves`, OFF (2026-10-14), so the wave pipeline (`BlackHoleWaves`, the lens shader's wave loop, `PrismGravityWarp.hlsl`'s strain, ~11 `BlackHoleConfig` wave fields) runs for nobody, and `studio_to_unity/stoat.json` still clones the studio's wave keys into it. Decide: keep as an opt-in look (and say so on the studio page), or retire the pipeline with its studio rows. | `grep -rn "BlackHoleWaves.Spawn" Assets --include=*.cs` → 3 sites, all inside `if (config.AnnihilationWaves)` |
| D14 | walked past | **`Tools/Shaders/verify_stoat_ripple.py` FAILS on bleeding-edge** under glslang 15.1 (`lens_vert.hlsl:9: 'declaration' : Expected`, both stages, before any change here); its negative control still "fires", so the harness reports FAIL while looking like it runs. The lens verifier's URP mock likely needs updating for this glslang. | `git stash; python3 Tools/Shaders/verify_stoat_ripple.py` → FAIL on `b645b9d3` |
| D15 | report (port) | **Amoebius's shim lacks** `Graphics.RenderMesh`, `Mesh.Clear(bool)`, `Mesh.SetColors(Color[], int, int)`, `Mesh.SetIndices(int[], int, int, …)`, `RenderParams.camera` and `Vector3.RotateTowards`; this branch wrote around them (vessel skill rule 51). A port-branch row, not a game change. | `dotnet build Port/src/CosmicShore.Live` on the first cut of this branch → 6 errors, all of these |
| D16 | report | **`STOAT.md` §1–§2 describe the round-4 orbit**, which no longer ships (Time mode is the catch-sling, §1b). The banner says so; the sections stay as the orbit's record. Fold them into a short history paragraph when the catch-sling settles in play. | `STOAT.md` banner, 2026-10-14 |
