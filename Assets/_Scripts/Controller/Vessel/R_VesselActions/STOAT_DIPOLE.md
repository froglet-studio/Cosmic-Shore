# Stoat — the Field Dipole (Space) and the Pathfinder (Time)

The round-15 Stoat, ported from the Stoat Flight Studio's field trajectory
(`Docs/Studios/StoatFlightStudio.html`, the `ft*` row; `Docs/Studios/README.md` round 15). It replaces the
round-4 orbit sling (`STOAT.md`) on the triggers: the sling's code and assets stay in the tree,
unbound, so rebinding `StoatSlingLeft/RightAction` restores it.

## 1. How it flies

**Space — the Field Dipole (LT / RT).** Squeeze a trigger and ONE sink–source pair (a black hole and a
white hole, each the other's throat) is laid **250 u** ahead, the poles together, in the frame the hull
had then. The triggers' **difference** pulls the poles apart sideways — the sink on the deeper
trigger's side — and their **sum** pulls them apart lengthways, the sink always the nearer pole:

| triggers | separation (sideways, lengthways) | reads as |
|---|---|---|
| one full | 200 u, 60 u | the sink to that side, a slight diagonal |
| both full | 0, 120 u | the sink dead ahead, the source 120 u beyond it |
| let go of both | → 0 | the poles fall together and **annihilate** when the horizons touch |

The poles' strength and size never follow the triggers — only their separation (the dipole moment)
does. Space sizes them: horizon **3.5 u** and GM **120 000** at rest, ×1 → ×2 at level 10
(`StoatDipoleConfigSO.poleSize`, read live on the owner, replicated as the horizon). The sink grows in
over 0.9 s, the source over 1.1 s (from 55%), so a fresh pair pulls before it pushes.

**The field** is the studio's: the sink's Paczyński–Wiita pull `GM/(r − r_s)²`, the source's softened
push `GM·r/(r² + ε²)^1.5` (ε = 2 source horizons), the sum capped at 3000 u/s². One step of flight in it
(`StoatDipoleMath.Step`, the studio's `fieldSubstep`) turns the nose by the field's perpendicular part
(capped at 12 rad/s) and carries a **gravity speed** along the nose from its parallel part (gripped back
toward cruise at 0.5 /s, ceiling 4 × cruise). Fly into the sink's horizon and you come out of the source
at the point reflection of where you went in (`BlackHolePairMath.ExitPosition`), with the gravity speed
you had before the dive.

**Time — the Pathfinder (passive).** Every frame the hull's own flight is run forward 600 u on the
inputs it holds (`StoatDipoleMath.PredictPath`, the studio's `fieldPath`: the same `Step`, the held
steering rates, through the wormhole) and drawn for the pilot as dots from 8 u ahead of the nose:
4 px dots, 10 diameters apart, **blue-grey** while open. When the poles **warp** the path at all — it
bends ≥ 3°, or goes through the wormhole — it turns **lime** and the hull flies down it faster: a warp of
the hull's own flight clock (`VesselTransformer.FlightTimeScale`), so the path stays the path and only
arrives sooner. The boost rises at 20 /s and fades over 0.6 s. **Time is rate:** ×2 at rest, ×3 at level 5
(the studio's number), ×4 at level 10 (`StoatDipoleConfigSO.boost`, floored at 1). A path that comes back
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

## 2. What the sink takes

| crosses the sink | what happens | where |
|---|---|---|
| **the owner** | carried out of the source (the boost line) | `StoatDipoleExecutor.FlyField`, `BlackHoleVesselPull.TryCarryThrough` |
| **a rival vessel** | carried out of the source, **stripped of its crystals** — every element's takeable level (`crystalStripShare` 1), left as crystals on the sink's side | `BlackHoleCrystalStrip.Levy` from `TeleportContinuity` |
| **a teammate** | carried through, not stripped | — |
| **a prism** | **vanishes** (destroyed at the horizon, scored to the owner, no debris — it fell behind the shadow); with **Space 5 (Theft)** it is **stolen**: carried out of the source in the owner's domain | `BlackHoleGravityField.ApplyVerdicts`, `Prism.Vanish` / `Prism.Steal` |
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
(xyz + horizon) / `NetStoatDipoleSource` — the `NetEchoSightShape` precedent: the separation is two
analog triggers no peer receives, and the size is Space-scaled on an unreplicated level. Every peer
lays its own copy of the pair from those and nothing else, and applies the owner's rules to it (the
upgrade bit, the name and the domain are replicated). The boost and the field flight run where the hull
flies. Known limits, the platform's own: the gravity field's prisms and the stripped crystals are
local per peer (the wormhole toll's stance), so two peers can disagree on a prism at the horizon's edge.

**The hole budget is FOUR, so only TWO Stoats can hold a pair at once.** `BlackHoleConfig.maxBlackHoles`
is clamped to `BlackHolePhysics.NativeWells.Capacity` (4), which the lens shader's well bank is sized
to. A third Stoat's squeeze is refused (`BlackHoleRegistry.CanSpawn`, a quiet verbose line), and each
peer applies the budget to its own copies in its own order, so in a 3–4 Stoat race two peers can
disagree on whose pair exists. This bites Warpline and Slingshot at 3+ Stoats; the round-4 sling had
the same ceiling. Raising it is a lens/shader change (the bank and the well job together), not a
config edit — logged as a follow-up, not done here.

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
opened wider than the studio's (200 u, not 70). `keySqueeze` 0.5 is the studio's.

**The path is in the scene.** `StoatPathfinderWorldDots`: camera-facing discs every `worldDotSpacing` (3 u: denser
than the studio's 2D line) along the 3D path, `worldDotSize` 0.7 u, never under `worldDotMinPixels` 2.5 px, one
instanced draw on `Sprites/Default` (always included in builds). They recede, pass behind prisms and bend through
the lens. `dotsInWorld` off brings back the studio's flat screen dots.

**Tune it in Unity:** FrogletTools ▸ Vessels ▸ Vessel Studio ▸ the Stoat card ▸ TUNE IN UNITY (`VESSEL_STUDIO_PLAN.md` §2).

## 4. Files

| | |
|---|---|
| Maths (pure, tested) | `StoatDipoleMath.cs` — `StoatDipoleTests` |
| Placement + field flight + owner rules + AI | `Executors/StoatDipoleExecutor.cs` |
| Pathfinder + boost + dots | `Executors/StoatPathfinderExecutor.cs`, `StoatPathfinderDots.cs` (screen overlay, after the lens) |
| Config | `Data Containers/StoatDipoleConfigSO.cs` → `_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset` |
| Triggers | `Data Containers/StoatDipoleActionSO.cs` → `StoatDipoleLeftAction` / `StoatDipoleRightAction` |
| Platform hooks | `VesselTransformer.FlightTimeScale`, `BlackHole.PrismCapture` / `CrystalStripShare` / `DomainTint`, `Prism.Vanish`, `BlackHoleCrystalStrip`, `R_VesselActionHandler.NetStoatDipole*` |
| Prefab + assets | `Tools/Build/author_stoat_assets.py` stages 4-5 (`--check`, `--self-test`) |
| Camera | `_SO_Assets/Camera/StoatCameraSettingsSO.asset` (stage 5) |
| Arcade | `Arcade/WARPLINE.md` (the Time race), `Arcade/SLINGSHOT.md` (now flown on the dipole) |

Audio slots ship **empty** (`openEvent`, `annihilateEvent`, `warpEvent` on the config).

## 5. Element map

| Element | Ability | Input | L5 upgrade |
|---|---|---|---|
| **Space** | Field Dipole — pole size ×1 → ×2 | LT / RT | **Theft** — swallowed prisms are stolen, not destroyed (`IsUpgradeActive(Space)`) |
| **Time** | Pathfinder — warp boost ×2 → ×4 | passive | open — proposals below |
| **Charge** | open — proposals below | — | — |
| **Mass** | open — proposals below | — | — |

## 6. Proposals (NOT implemented — mark up)

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
of the prediction near the sink (≤ 96 substeps per 3 u step, ~200 steps).
