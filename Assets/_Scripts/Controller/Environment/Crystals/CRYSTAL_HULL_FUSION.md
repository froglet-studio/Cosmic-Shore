# Crystal → hull fusion

**The crystal does not fly into the vessel and vanish. It becomes part of the hull.**

The generic elemental capture (`CrystalCaptureConfigSO`) snatches the crystal, sucks it into the
hull centre, shrinks it to nothing and sprays a husk into the wake. It reads as "a thing
disappeared". A fusion replaces that, per **(vessel, element) pair**, with the crystal visibly
joining the hull:

1. **Approach** (0.22 s) — the crystal, still whole, is pulled onto the side of the hull it came
   from, shrinking and turning with the vessel as it closes.
2. **Wrap** (0.34 s) — it opens. Each of its rigid plates slides round the hull to its own spot:
   the plate that touched stays at the contact, the plates on the crystal's far side wrap round to
   the hull's far side. Plates near the contact move first. Colour carries from the pickup's lime
   to the pilot's own domain crystal pair.
3. **Hold** (0.24 s) — the plates sit flush on the skin, flare, and the charge discharge fires
   on every edge continuously. The pickup sound plays here.
4. **Sink** (0.30 s) — the plates flatten into the hull and dissolve.

**First and only pair today: Squirrel × Charge** — the experiment. Every other pair plays the
generic capture, unchanged. Adding a pair is one entry in `Resources/CrystalHullFusionConfig`.

---

## 1. Why the charge crystal first

It is the one elemental crystal with **static geometry**: 60 pentagonal prisms on one shell, no
blend shapes (Space pulses its blend shapes; Time flips its blocks). Measured off
`ChargeCrystalExport1_7-11-25.fbx`: exactly 60 solids, 7 faces each, every centroid at radius
0.9406 and every half-extent 0.3660 model units. So the crystal *already is* a set of plates; the
fusion only has to move them.

Plates are moved **rigidly** (plus a uniform-in-plane scale and a flatten along their own normal).
A plate never deforms, so its faces stay planar and the charge shader's crease-edge bolts keep
running on the hull: `CrystalEdgeArcMeshBaker` bakes that edge data per triangle in model-radius
fractions, and a rigid plate carries it along.

## 2. Where each plate lands

Two steps, and the order matters (`CrystalHullFusionGeometry`):

- **Spots first.** The contact spot is the outermost outward-facing hull vertex in the direction
  the crystal came from (read in the hull's normalised space — each axis over its extent). From it,
  the other 59 spots are **farthest-point sampled** over the outward-facing skin, so they sit at
  near-uniform spacing on any hull shape.
- **Then a match.** Each plate's target direction is its crystal radial reflected through the plane
  normal to the contact (`WrapDirection`: contact plate → contact, far plate → antipode, everyone
  keeps their angular distance). Plates are matched to spots by the **optimal assignment**
  (Hungarian, cost `1 − cos`), not greedily.

**Measured against the shipped Squirrel FBX** (offline port of the same algorithm, three approach
directions):

| Approach | Spot gap min / median | Plate → spot alignment, worst / median |
|---|---|---|
| Mapping each plate's direction straight onto the hull (first attempt) | **0.00** / 0.24 — 9 of 60 plates on a spot another plate held, piled on the wing tips | — |
| Spread + greedy match | 0.46 / 0.52 | **−0.94** (a plate crossing the whole hull) / 0.96 |
| Spread + optimal match (shipped) | 0.46 / 0.52 | **+0.08** / 0.88 |

The hull units are the FBX's own (Squirrel extents 2.25 × 3.23 × 0.73). Plate footprint radius is
`tileFill × gap / 2`, so neighbours meet at the tightest spacing (`tileFill` 1) or overlap into a
continuous skin (1.1, shipped).

## 3. The plates ride the bones

The Squirrel hull is skinned and puppeteered. At collection the hull is baked
(`SkinnedMeshRenderer.BakeMesh`) in its current pose — element blend shapes included — and each
plate's spot is pinned to the bone that dominates that vertex. During the wrap the path is planned
against the baked hull and handed over to the live bone as the plate arrives; from the hold on, the
plate is wherever its bone is. A plate on a wing stays on the wing while it flaps.

That needs the hull mesh **CPU-readable** (bone weights). This branch turns on Read/Write for
`SquirrelVessel_CosmicShoresTest1.fbx` (`isReadable: 1`) — one more CPU copy of a 13k-vertex mesh.
An unreadable hull still fuses, pinned to the renderer instead, and says so once.

## 4. Hook and retirement

`ElementalCrystalImpactor.RunCapture` → `TryFuseOntoHull`: if the config lists
`(vesselStatus.VesselType, crystal element)`, `CrystalHullFusion.Begin` copies the crystal's models
(mesh clone with every UV channel, shared materials, property block), lays out the fusion, hides
the crystal's renderers and draws frame 0 the same frame. The crystal stays alive, hidden, until the
**clamp**, when it is moved to the contact point, plays its pickup sound via
`Crystal.Explode(SuppressHusk = true)` and leaves the cell (`DestroyCrystal`). No husk spray: the
body is on the hull, not in the wake.

Scoring and the element level are untouched — both land at contact, before any of this
(`CollectBy`). The fusion is pure photons.

Every refusal falls back to the generic capture and is warned once per reason: no hull renderer,
an empty bake, a crystal with no readable model.

## 5. Why CPU and not a shader stamp

`ScarabCrystalMorph` runs its geometry in the vertex stage off `_PrismClock`, per the prism
clock-material law. This one does not, deliberately:

- the target **moves** (a skinned hull at flight speed, bone by bone), so a stamped target in a UV
  channel would be stale the frame after it was written;
- it is a **one-shot per pickup** (~1 s, ~2.9k vertices, 60 plate poses a frame), not a standing
  per-prism cost — the law exists for the thousands of prisms, not for one crystal;
- the charge shader is a hand-written `.shader`; leaving it untouched means no existing crystal
  anywhere can change.

## 6. Files

| File | Role |
|---|---|
| `Controller/Environment/Crystals/CrystalHullFusion.cs` | runtime: adopt, plan, pose plates, write mesh + block |
| `Utility/CrystalHullFusionGeometry.cs` | pure: plate split, contact spot, farthest-point spots, assignment, wrap |
| `ScriptableObjects/CrystalHullFusionConfigSO.cs` | per-(vessel, element) entries + beat timing |
| `Resources/CrystalHullFusionConfig.asset` | the opt-in: Squirrel × Charge |
| `ImpactEffects/Impactors/ElementalCrystalImpactor.cs` | `TryFuseOntoHull` / `RetireIntoFusion` |
| `Environment/FlowField/Crystal.cs` | `TryGetDomainCrystalColors` (the pilot's crystal pair) |
| `_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx.meta` | `isReadable: 1` |
| `Tests/Editor/CrystalHullFusionTests.cs` | plates, spots, assignment, wrap, beats, shipped config |

## 7. Tuning knobs (`Resources/CrystalHullFusionConfig`, per entry)

| Knob | Shipped | What it does |
|---|---|---|
| `approachSeconds` / `wrapSeconds` / `holdSeconds` / `sinkSeconds` | 0.22 / 0.34 / 0.24 / 0.30 | the four beats (1.10 s total; generic capture is 0.44) |
| `landRadiusFraction` | 0.35 | crystal size on landing, × hull mean half-extent |
| `approachAcceleration` | 2.4 | pull curve exponent |
| `approachPop` | 0.25 | swell at the start of the pull |
| `approachSpinTurns` | 1 | whole turns on the way in (whole — the wrap is planned against the collect orientation) |
| `tileFill` | 1.1 | plate footprint vs the gap to its neighbour |
| `flatten` | 0.45 | landed plate thickness vs the crystal's prisms |
| `wrapLift` | 0.3 | how far plates bow off the skin mid-wrap (normalised radius) |
| `wrapStagger` | 0.4 | contact-first travelling open; 0 = all at once |
| `spotConeDegrees` | 12 | cone the contact spot is searched in |
| `surfaceLift` | 0.05 | gap to the skin in plate thicknesses (anti z-fight) |
| `flareGain` | 2.6 | brightness at the clamp (hue kept) |
| `clampPulse` | 1.12 | swell on the clamp |
| `arcBoost` / `holdArcDuty` | 2 / 0 | charge discharge intensity × and silence during the hold |
| `sinkDepth` | 1 | plate thicknesses sunk by the end |
| `convergeToDomainColour` | on | lime pickup → pilot's domain crystal pair over the wrap |

## 8. Cost

Per pickup, once: one `BakeMesh` (~13k vertices), farthest-point sampling of 60 spots over ≤4096
candidates (~0.25M distance updates), a 60×60 Hungarian (~0.2M steps), one mesh clone. Per frame
for 1.1 s: 60 plate poses and one vertex + normal upload of ~2.9k vertices. Not profiled in the
editor yet — if a crowd of AI Squirrels in a crystal-heavy mode makes it show, the bake and the
spot spread are the candidates to cache per hull pose.

## 9. Verification status

- **Compiles** against real Unity 6000.0 references, player and editor configs
  (`Tools/Build/unity_refcompile`), negative-controlled with a planted missing member in
  `CrystalHullFusion.cs`. The new asset passes `check_generated_assets.py` (negative-controlled with
  a misspelled key).
- `AssignMinCost` matches brute force on 300 random matrices (n ≤ 7), run standalone.
- The spot layout was simulated against the real Squirrel mesh (table in §2).
- **Not yet seen in the editor.** Nothing visual here has been looked at.

### In-editor verification

1. Any scene with charge crystals (freestyle / a cell with lifeforms) → fly a **Squirrel** and skim
   a **charge** crystal (the 60-plate one with the crackling edges).
   - Expect: the crystal is pulled onto the hull side it came from, opens, its plates slide round
     and lie flat over the whole hull (top, bottom, wings), flare and crackle, sink in. ~1.1 s.
   - The plates should end in your **domain** colour, not lime.
   - Pickup sound on the clamp, not at contact. No husk spray.
2. Pitch/yaw hard during a fusion — plates on the wings should stay on the wings.
3. Collect a **mass/space/time** crystal with the Squirrel, and a charge crystal with any other
   vessel — the old capture, unchanged.
4. Console: no warnings. Turn on **FrogletTools > Toolbox > Logging > CrystalMorph** to see one
   `[CrystalHullFusion]` line per pickup (plate count, footprint scale, domain colour read).
5. Tuning order if it reads wrong: timing first (§7 beats), then `tileFill` / `flatten` (how much
   of the hull it covers and how flush), then `wrapLift` (plates clipping through the hull mid-wrap
   → raise it).

## 10. Follow-ups / known limitations

- **Wrap paths are planned on the hull's bounding ellipsoid**, not its real surface. A plate bows
  out by `wrapLift` mid-flight to clear the skin; on a deeply concave hull it can still cut a corner.
- **The hull's charge blend shape glides while the plates sit on it** (the level-up lands at
  contact). Plates are pinned to the pre-glide surface, so they may sit a hair off it by the sink —
  the morph moves Squirrel vertices by a few percent.
- **One fusion per pickup, no pooling.** A burst of pickups makes a burst of mesh clones.
- **Other pairs.** The mechanism is generic over any crystal whose mesh splits into solids
  (the omni cage is 122; Time is 30 blocks but animated — its fusion would need to sample the
  animated pose first). Space is one connected body; it needs its own idea, not this one.
