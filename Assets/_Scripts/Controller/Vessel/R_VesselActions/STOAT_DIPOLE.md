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
(xyz + horizon) / `NetStoatDipoleSource` (xyz + the pair's serial in w: a press can close one pair and
open the next inside one tick, so a peer re-lays its copy when the serial changes instead of sliding the
old pair across) — the `NetEchoSightShape` precedent: the separation is two
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

## 4. Files

| | |
|---|---|
| Maths (pure, tested) | `StoatDipoleMath.cs` — `StoatDipoleTests` |
| Placement + field flight + owner rules + AI | `Executors/StoatDipoleExecutor.cs` |
| Pathfinder + boost + dots | `Executors/StoatPathfinderExecutor.cs`, `StoatPathfinderDots.cs` (screen overlay, after the lens) |
| Config | `Data Containers/StoatDipoleConfigSO.cs` → `_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset` |
| Triggers | `Data Containers/StoatDipoleActionSO.cs` → `StoatDipoleLeftAction` / `StoatDipoleRightAction` |
| Platform hooks | `VesselTransformer.FlightTimeScale`, `BlackHole.PrismCapture` / `CrystalStripShare` / `DomainTint`, `Prism.Vanish`, `BlackHoleCrystalStrip`, `R_VesselActionHandler.NetStoatDipole*` |
| Prefab + assets | `Tools/Build/author_stoat_assets.py` stage 4 (`--check`, `--self-test`) |
| Arcade | `Arcade/WARPLINE.md` (the Time race), `Arcade/SLINGSHOT.md` (now flown on the dipole) |

Audio slots ship **empty** (`openEvent`, `annihilateEvent`, `warpEvent` on the config).

## 5. Element map

| Element | Ability | Input | L5 upgrade |
|---|---|---|---|
| **Space** | Field Dipole — pole size ×1 → ×2 | LT / RT | **Theft** — swallowed prisms are stolen, not destroyed (`IsUpgradeActive(Space)`) |
| **Time** | Pathfinder — warp boost ×2.5 → ×5 | passive | open — proposals below |
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
