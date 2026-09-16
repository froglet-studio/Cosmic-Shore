# The vessel's elemental-crystal absorb morph — SPIKE

**The crystal does not shrink away. Its cage opens onto the hull, and the ship takes it.**

An elemental crystal collected by a vessel that carries `VesselCrystalAbsorbMorph` retires by
carrying its own BODY onto the ship: the cage opens out across the suction flight onto the sphere
that circumscribes the hull, arrives fully formed exactly as the absorb begins, and dissolves off
the ship over the absorb's existing opacity ramp.

This is the **third** instance of a per-vessel crystal retirement and the **first on the hull
path**. The Squirrel's ring morph and the Scarab's ball morph (`SCARAB_CRYSTAL_MORPH.md`) both
retire an OMNI crystal onto a thing the pickup MADE. Here the pickup makes nothing, so there is no
second object to hand off to — the crystal is morphed **in place** and the vessel is the target.
`SCARAB_CRYSTAL_MORPH.md` §9 names this as the missing piece:

> *"The hull path is not wired on this branch. The Squirrel's branch carries
> `VesselOmniCrystalRetirementSO` + the `VesselImpactorDataContainerSO` slot + `OmniCrystalImpactor`'s
> skip, which is how a HULL-collected crystal gets a retirement."*

Measured: **none of those three files exists in any ref of this clone.** The hull door was designed
on a branch that never landed, so this builds it from the platform pieces that did.

**⚠ This is a SPIKE and it has never been run in the editor.** It is scoped to one element and one
hull on purpose — see §7 for exactly what is unverified and §6 for the look call it exists to put
in front of a playtest.

---

## 1. Why the scope is Mass and the Rhino

**The four elemental crystals are on four different shader families, and only one carries the
morph splice.** Measured off the shipped prefabs:

| element | shader | `_CrystalMorph` spliced? |
|---|---|---|
| **Mass** | `ShepardGraph.shadergraph` | **yes, today** |
| Charge | `ChargeCrystal.shader` (hand-written ShaderLab) | no |
| Space | `CrystalGraph.shadergraph` | no |
| Time | `DynamicFresnelGraph` + `InverseDynamicFresnelGraph` | no |

`CrystalMorph.hlsl` is spliced into `ShepardGraph` and nothing else — which was enough for the two
shipped morphs because both retire the OMNI crystal, whose cage draws on that same graph. So Mass
is free and the other three each need their own splice.

The gate in code is a **capability test, not an element test**: `VesselCrystalAbsorbMorph.CanMorph`
asks the shell's material whether it carries `_CrystalMorph`. Charge/Space/Time therefore switch
on the day their graph is wired, and nothing here has to learn their names.

**The Rhino** is the part-per-mesh family (8 MeshFilters, **0 skinned**), which sidesteps the
skinned-hull measurement trap in §2 for a first look.

---

## 2. Why the target is a SPHERE, not the hull mesh

`CrystalMorphMeshBuilder.ConvexHullTarget` is exact *"for any **convex** target"*. A vessel hull is
neither convex nor stable:

- **Not convex.** `LandOnHull` rays from the hull centre and takes the first facet the ray exits
  through, so a vertex aimed at a wingtip exits the **fuselage** first and lands *inside* the ship.
- **Not stable.** Five of eight hulls are **skinned**, so `sharedMesh` is the bind-pose mesh in
  ROOT-BONE space — the trap `Docs/VESSEL_CONSTRUCTION.md` records twice (the Sparrow shipped a
  ~5× oversized occlusion corridor that way).
- **Moving while you land on it.** Every hull morphs its blend shapes on the very element the
  crystal just raised, over the fleet's shared morph feel — so the surface is deforming during the
  0.26 s the cage is landing on it.

So the target is the sphere that **circumscribes** the hull, measured by the corridor's own
`PrismOcclusionCorridor.MeasureCircumscribedRadius` (hull only, rotation-invariant, skimmers and
disabled renderers excluded). One measurement, correct on any hull, convex by construction, and it
is the same number the prism occlusion corridor and the Echo Sight halo already size themselves
from.

### The load-bearing detail: the sphere is centred on the CRYSTAL's local origin

Not on the vessel. A sphere about the crystal's own local origin is invariant under the crystal's
rotation **and** its motion, so **one bake survives the whole flight**: the capture's suction lerp
carries the crystal to the ship and the morph independently opens its cage, and the two compose
with nothing to re-bake and nothing to double-count.

Centre it on the *vessel* instead and the targets sit hundreds of units away in the crystal's local
space, so the cage smears across the gap the suction is already closing. A hull MESH would need
re-baking every frame as the ship turned — 360 vertices cast against 320 facets, per frame, per
pickup. **This is the property that makes a sphere the right proxy rather than a lazy one.**

---

## 3. Why the window is the SUCTION, and why the transform has to stop

**The absorb happens at the vessel's own origin** (`position = target`), inside an opaque
z-writing hull, and the crystal's shells are alpha-blended and non-z-writing. A fold confined to
the absorb would be **invisible**. So the morph runs across the **suction** (0.26 s of the 0.44 s
beat) and lands at absorb entry, leaving the absorb's existing opacity ramp to dissolve a shell
that is fully formed. That is the Scarab's `morphFraction` split, expressed in the phases the
capture already has.

**And the transform must stop scaling.** `CrystalCaptureConfigSO.ScaleMultiplier` drives to **zero**
through the absorb — the shipped crystal *shrinks* into the hull. Left running, that scales the
baked targets with it and the shell collapses to a point whatever the shader does. A morphing
capture therefore **holds** the crystal's scale at its snatch-end value (1.5× base) and lets the
cage carry the entire size change: the same division the Scarab makes, and the better read anyway.

One ordering detail is load-bearing: the held scale is **seated on the transform before the bake**,
because `TryBegin` reads the crystal's *current* local space. The phase curve happens to be
continuous across the snatch→suction boundary, so the value would have matched anyway — but a bake
that is only correct because of that is a bake waiting for someone to retune `snatchScale`.

---

## 4. The finding worth more than the feature: the stagger was float noise

`CrystalMorphMeshBuilder` phases each welded **solid** by its distance from the centre and
normalises across the observed range, so phase carries information only when the cage has solids at
**different radii**. The omni cage does — struts and panels — which is why *"the outermost struts
leave first"* reads there.

**An elemental cage does not.** Measured by running the shipped builder on the shipped
`MassCrystalExport1_8-21-25.fbx`:

- the cage welds into **60 connected solids**;
- their mean radii are **identical to within ~1.4e-6** (all 0.9990);
- the builder's own `span = Mathf.Max(1e-5f, maxR - minR)` floor then divides that float noise by
  the epsilon and emits a phase band of **[0.857, 1.000] across 16 distinct values**.

It looks exactly like a cascade. Nothing authored it, it is not the omni look it would be
borrowing, and it is not reproducible in principle. So the hull path passes **one phase for every
solid** (`TryBuild(..., 0f, 0f)` and `stagger = 0` in the stamp), which makes the shader's stagger
term identically zero however `CrystalMorphConfig` is tuned — the honest motion for a shape that
has no outermost part.

`VesselCrystalAbsorbMorphTests.StaggeringASingleRadiusShellIsFloatNoise` is the negative control
for that decision, and `ShippedElementalCageIsASingleRadiusShell` fails the moment somebody
re-exports the cage with genuine radial structure, so the decision gets revisited rather than
silently continuing to be wrong in the other direction.

> **General rule:** *a normalisation guarded by an epsilon floor does not fail on degenerate input
> — it emits a plausible spread of noise.* When a value is normalised across a measured range,
> assert that the range is real before trusting what comes out.

---

## 5. Files

| file | role |
|---|---|
| `VesselCrystalAbsorbMorph.cs` | the whole feature: opt-in marker, hull measurement, target bake, one stamp |
| `ElementalCrystalImpactor.cs` | **+30 lines**: begin the morph at suction entry, hold the scale, destroy the morph mesh in a `finally` |
| `_Scripts/Tests/Editor/VesselCrystalAbsorbMorphTests.cs` | 5 tests; the geometry and the §4 finding |
| `_Models/MassCrystalExport1_8-21-25.fbx.meta` | `isReadable: 0 → 1` (see below) |
| `_Prefabs/Spacevessels/Rhino.prefab` | the component, on the root |

Reused unchanged: `CrystalMorph.hlsl`, `CrystalMorphMeshBuilder`, `IcosphereMeshGenerator`,
`CrystalMorphConfigSO` (`Resources/CrystalMorphConfig` — the fleet's single source for the FEEL,
exactly as the capture beat is), `PrismOcclusionCorridor.MeasureCircumscribedRadius`, `PrismClock`.
**No shader was edited and no shipped prefab other than the Rhino was touched.**

### The one asset cost: `isReadable`

`CrystalMorphMeshBuilder.TryBuild` reads the cage's vertices on the CPU and refuses (named) on an
unreadable mesh. The Mass FBX shipped `isReadable: 0`; it is now 1. **This is an established cost,
not a new one** — `OmniCrystalExport1_8-21-25.fbx` already ships `isReadable: 1` for the two
existing morphs. It retains a CPU copy of a 360-vertex mesh.

### Opt-in is the per-vessel wiring

A vessel takes the morph by **carrying the component**, like a tail or a jet
(`Docs/VESSEL_TAIL_AND_JETS.md`). There is deliberately no container entry and no
`VesselClassType` check: an impact-effect container cannot carry it (the capture is run by the
*crystal's* impactor, not by a vessel effect), and a hardcoded class test would be a second
authority on which hulls opt in. A missing component is an inspectable state and the fleet default
is the shipped husk-and-shrink absorb, byte-for-byte unchanged.

---

## 6. The look call this spike exists to settle

**Measured, by running the shipped builder on the shipped cage:**

| `targetHullFraction` | landing radius | vs the cage's own 2.11 u | reads as |
|---|---|---|---|
| **1.00** (shipped default) | **18.43 u** | **8.72×** | a shell containing the whole ship, engines included |
| 0.55 | 10.14 u | 4.80× | a shell inside the body (body radius 16.06 u) |
| 0.25 | 4.61 u | 2.18× | a bead on the hull |

Reference measurements: Rhino hull circumscribing radius **18.51 u** (driven by `engine right`;
the body alone is 16.06 u). Mass cage world radius **2.114 u** at its shipped scales
(shell 1.38 × root 1.5).

**⚠ The thing to watch, and it is a real risk.** At landing, the four shells **converge on the same
targets**, so the concentric spread collapses into four *coincident* alpha-blended spheres — at
18.43 u, at `flareGain` 3× brightness, on the single brightest frame of the effect. This is the
`/vessel` and asset-surgery rule that *N instances of an additive effect sum, and the tuning
surface is the sum*: a per-shell judgement is structurally blind to it. If it whites out, the dial
is `targetHullFraction` and the table above is pre-measured so it is one sitting, not three.

The default is 1.00 because that is the literal ask — *the crystal's mesh becoming the vessel's* —
and a spike should show the strong version first.

**If the sphere reads as a bubble rather than as absorption**, the next step is a better convex
proxy, not a smaller sphere: an oriented **box** from the hull's measured bounds is convex,
trivially generated, and on a long thin ship reads far more like the vessel than a ball does. That
is ~20 lines against this same `ConvexHullTarget` seam and no other change.

---

## 7. Verification status

**🔴 NEVER RUN IN THE EDITOR.** Everything below was proven out of editor; none of it is a frame.

| gate | proves | result |
|---|---|---|
| Roslyn + faithful stubs | both changed files type-check, with the real `CrystalMorphMeshBuilder` / `IcosphereMeshGenerator` / both config SOs compiled in rather than stubbed | **0 errors** |
| …negative-controlled | a wrong member name, a wrong arity, an undeclared local **in a method body**, and a wrong call-site type each fire (CS1061 / CS1501 / CS0103 / CS1501) | 4/4 fire, file restored byte-identically |
| `VesselCrystalAbsorbMorphTests`, **compiled and RUN** | the cage lands ON the polyhedron at every fraction, the landing radius tracks `targetHullFraction`, targets wear their facet's outward normal (worst dot 0.9953), position and normal phases never drift, the shell moves as one | **5/5 pass** |
| …negative-controlled | an unscaled target matrix and a re-enabled stagger each fail exactly one test | 2/2 fire |
| shipped-cage assertion | run against the **real** Mass FBX geometry (extracted from the binary), confirming 60 solids at one radius | pass |
| prefab surgery, differential vs `HEAD` | documents 220 → 221, anchors 220 → 221, **zero new dangling fileIDs**, one `m_Component` entry, one anchor, one guid reference, no int64 overflow | clean |
| `field_parity.py` | all three serialized fields authored, **no unknown keys** | clean |
| guid uniqueness | each new guid owned by exactly one `.meta` | clean |
| the six `Tools/Build` gates | conditional compilation, using directives, self-referential locals, console logging, enum member refs, switch label collisions | all OK |

**What only a playtest can answer:** whether the open-out reads as absorption or as a burst;
whether 1.00 whites out (§6); whether 0.26 s is the right length; whether the shell wants to be
visible *behind* the ship (it is transparent and non-z-writing, so it will be occluded by the hull
from the front).

### In-editor verification

1. **Any mode with a Rhino and an elemental crystal** (Astro League, Peel the Cage; or the
   freestyle Lifeform Matrix toy — spawn a Mass lifeform and joust it). Collect a **Mass** crystal.
   Expect: the cage opens out into a shell around the ship across the flight, arrives at ~18 u
   radius, and dissolves. Total ≈ 0.44 s, unchanged.
2. **Collect a Charge, Space or Time crystal.** Expect the **ordinary** absorb, unchanged — those
   shaders carry no splice. One `CrystalMorph` verbose line says so.
3. **Any other vessel, any crystal.** Expect the ordinary absorb: no other prefab carries the
   component.
4. **Tune it** on `targetHullFraction` against the §6 table.
5. **Trace it** with FrogletTools ▸ Toolbox ▸ Logging ▸ `CrystalMorph`; every refusal names itself
   and falls back to the shipped absorb, so a refusal costs a flourish and never a pickup.
6. **Run `VesselCrystalAbsorbMorphTests`** in the Test Runner — it should be 5/5 in the editor too,
   and `ShippedElementalCageIsASingleRadiusShell` reads the real FBX there.

---

## 8. Follow-ups

- **Splice the other three graphs** (`ChargeCrystal.shader` is hand-written HLSL and is arguably
  the easiest of the three). `wire_crystal_morph.py` does this job for a `.shadergraph`.
- **The box/convex-hull target** (§6) if the sphere reads as a bubble.
- **Nothing is replicated, and nothing needs to be — today.** The capture is already per-machine
  (fauna are per-peer, so an elemental crystal is client-local), so this inherits whatever the
  shipped capture does. If elemental crystals ever become `NetworkSynced`, this needs the same look
  the Scarab's `n_ForgedFrom` gets.
- **The absorb's own opacity ramp still runs the shipped `EaseIn`**, so the shell dissolves from
  full in 0.1 s. If the landing wants to linger, that is a `CrystalCaptureConfigSO` change and it
  moves every vessel — say so before touching it.
- **`CSLogChannel.CrystalMorph`'s label still says "omni-crystal retirement steps"** and now also
  carries the elemental hull path. One-line asset-free fix in `CSDebug.cs`.
