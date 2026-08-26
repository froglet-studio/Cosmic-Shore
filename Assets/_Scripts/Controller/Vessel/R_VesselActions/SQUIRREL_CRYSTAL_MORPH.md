# The Squirrel's omni-crystal morph

**The crystal does not shatter. It becomes the ring.**

A Squirrel that flies through an omni crystal lays a ring of eight shielded prisms just ahead of
its nose. Until now the crystal *also* burst into the shared spent-crystal husk spray, so the
pickup read as two unrelated events: something exploded, and separately some prisms appeared. It
now reads as one — the crystal's cage opens, its plates fly outward, and they land as the eight
octahedra of the ring it just made.

This is the first instance of a **per-vessel omni-crystal retirement**: the shared husk spray is
the platform default, and a vessel may replace it with its own animation as part of its vessel
package. The Squirrel is the first hull to carry one; every other hull keeps the husk until it is
given one, so the fleet migrates one vessel at a time.

---

## 1. Why the mapping is exactly 1:1

This is the fact the whole animation is built on, and it is a measurement, not a design choice.

The omni crystal's model (`Assets/_Models/OmniCrystalExport1_8-21-25.fbx`) is a cage of **122
disjoint solids**:

| solid | count | faces each | total |
|---|---|---|---|
| box strut | 90 | 6 quads | 540 quads |
| triangular prism | 20 | 2 **triangles** + 3 quads | **40 triangles**, 60 quads |
| pentagonal prism | 12 | 2 **pentagons** + 5 quads | **24 pentagons**, 60 quads |
| | | | **64 non-quad faces**, 660 quads |

A shielded prism renders as a circumscribing octahedron (`OctahedronMeshGenerator`), which has
**8 triangular faces**. Eight of them is **64 faces**.

> **40 + 24 = 64 = 8 × 8.** Every panel of the crystal becomes exactly one face of a prism, with
> nothing invented and nothing spare.

The 660 quads — the struts, and the prisms' rims — are the leftovers. Each collapses to a point
inside whichever octahedron its own solid was assigned to, and is absorbed by it. They are stamped
to go **first**, so by the time the panels land there is nothing left hanging around the shape
they were absorbed into.

Proven against the shipped FBX by `Tools/Build/measure_omni_crystal_morph.py`, which also renders
the choreography (`--render`).

---

## 2. The three things that make it seamless

**1. It draws the crystal's own renderers.** The morph object copies the crystal's four shell
models — mesh, shared materials and MaterialPropertyBlock — so its first frame *is* the crystal,
including the Shepard shells' band animation and `Crystal.ApplyColorSetTint`'s collectability
colour. A rebuilt look-alike would pop on frame 0, which is the one frame that has to be free.

**2. It ends ON the real prisms.** The targets are read from the prisms `BoostRingBuilder`
actually laid — each one's own shield semi-axes and its own final pose — so the last frame of the
morph and the first frame of the ring are the same geometry. There is no second authority to
drift from: retune `SpawnableRings` and the animation follows for free.

**3. The prisms are laid at once, and only their PHOTONS wait.** Colliders, mass, shield state
and spatial-index registration all go final the instant the ring is laid — *the ring is skimmable
from frame 0 while the morph is still in flight* — and `Prism.SetVisualStandIn` holds nothing but
their rendering. That is the clock-material law's own division (`Docs/PRISM_ANIMATION.md` §4)
applied to a hand-off: gameplay final at the start, only photons animate.

**4. The swap happens ONLY between equivalent states.** The prisms are revealed at `t = 1` — not
before — when the morph's geometry *is* their octahedra (same corners, same orientation) and its
colour has been carried onto their shielded palette. There is no cross-fade between two
different-looking things, because there is no moment at which they look different. The colour
target is read off the material the laid prisms actually bound (`_DarkColor`/`_BrightColor`, the
prism's base face and fresnel rim), lerped from what each crystal shell is drawing
(`_DullCrystalColor`/`_BrightCrystalColor` — the same two roles). `handoffFraction` can reveal
them earlier, but only as a debugging aid: below 1 it swaps while panels are still arriving,
which is the seam this design exists to remove.

---

## 3. How it runs

```
vessel hits omni crystal
  ├─ OmniCrystalImpactor.ExecuteEffect            [server]
  │     sees the vessel carries an OmniCrystalRetirement → SKIPS Crystal.Explode
  │
  └─ VesselImpactor.ExecuteOmniCrystalImpact      [owner → server → EVERY peer]
        ├─ SquirrelCrystalMorphByCrystalEffectSO  (the retirement, runs FIRST)
        │     └─ SquirrelCrystalMorph.Begin(...)  stands up the shells, waits for the ring
        └─ VesselExplosionByCrystalEffectSO       (the sibling that lays the ring)
              └─ AOEShieldedRingSpawner → SpawnableRings → BoostRingBuilder.LayRing
                    └─ BoostRingBuilder.RingLaid ──► SquirrelCrystalMorph.OnRingLaid
                          ├─ CrystalMorphMeshBuilder.TryBuild  (one mesh, targets in TEXCOORD2)
                          ├─ prism.SetVisualStandIn(true)      (photons only)
                          └─ ONE stamp: _CrystalMorph = (PrismClock.Now, duration, stagger)

  t = 0.72 · duration : the ring is revealed and the morph dissolves out over it
  t = 1.00 · duration : the morph is destroyed
```

**Replication needed no new networking.** The vessel's crystal effects are already broadcast by
`NetworkVesselImpactor`, so the morph plays on every machine exactly like the husk it replaces.
The suppression half is server-only and needs no RPC of its own — the server is the only machine
that reaches `Crystal.Explode`'s broadcast in the first place.

**The morph does not lay the ring, and must not.** The ring belongs to the sibling explosion
effect and the ordinary AOE spawner; the morph listens for whatever that lays. One authority for
the ring. If no ring arrives within the grace window (a mode that wires none, a spawner that
declines), the morph fades the crystal's body out instead of hanging there — continuity of
existence holds either way.

---

## 4. The geometry, and the two traps it is written around

`CrystalMorphMeshBuilder` emits the source mesh **vertex for vertex**, unshared (one vertex per
triangle corner), plus one extra attribute: **TEXCOORD2 = (target position, phase)**. The vertex
stage lerps position → target off `_PrismClock`, so the whole animation is one stamp and zero
per-frame CPU.

**Trap 1 — a face is found STRUCTURALLY, never by coplanarity.** 60 of this cage's quads are
non-planar by about 5°, so a plane test cuts them in half and reports **160** triangle panels
where there are 40. That happened on the first pass here. Two triangles cut from one imported
polygon reference the very same vertex INDICES, and two from different polygons cannot, because
the importer split those corners apart. That holds by measurement on this model: all 724 polygons
carry a *single* normal across their corners (max intra-polygon normal angle 0.000°), so no
polygon is split internally and none weld together.

**Trap 2 — a panel must BECOME its face, not sit inside it.** The corner map anchors three source
corners to the target's three corners and slides the rest along the edges between them. The first
version mapped by raw perimeter fraction, which is corner-to-corner only when the two polygons
happen to share edge proportions — and an octahedron face here is 2.7 × 2.7 × 11.25, nothing like
the cage's near-equilateral panels. Measured, that put only **83 of 336** panel corners on a
target corner: every panel landed as a smaller triangle inscribed in its face, and the finished
octahedra would have read as shrunken plates with gaps at the seams. Anchored, a panel's outline
follows the target's exactly and its **area equals the face's area exactly** — a pentagon's two
non-anchor corners ride *on* the edges, so they add vertices without changing the shape.

A **solid**, by contrast, is found by welded POSITION. That is the opposite grouping and it is
what keeps one strut's six faces travelling to the same octahedron.

---

## 5. The shader

`CrystalMorph.hlsl` is spliced into **`ShepardGraph`** — the shader every omni-crystal shell
renders with — by `Tools/Shaders/wire_crystal_morph.py`, at the very **END** of the vertex chain:

```
<vertex chain>.Out -> CrystalMorph.Position          UV2 -> .Target
_PrismClock -> .Clock                                _CrystalMorph -> .Morph
CrystalMorph.Out -----------------------------------> VertexDescription.Position
```

Last is load-bearing in both directions. ShepardGraph's shells displace along the normal, so
splicing after the displacement means t = 0 is the crystal *including* that displacement, and the
lerp to the bare target removes it by t = 1 — which is what lands the shape exactly on the
octahedra instead of hovering a shell above them.

`_CrystalMorph` is `(start time, duration, stagger)`, written per renderer through a
MaterialPropertyBlock. **Duration 0 is "unstamped" and returns the position untouched**, so every
crystal material in the project carries this node and is bit-identical to before it existed.

Two properties were added to ShepardGraph: `_PrismClock` (unexposed — the global the clock's
publisher writes once per frame, from the same value the stamp uses) and `_CrystalMorph` (exposed,
so a MaterialPropertyBlock can reach it).

---

## 6. What is verified, and how

| gate | what it proves | run |
|---|---|---|
| `Tools/Build/measure_omni_crystal_morph.py` | the 40 + 24 = 64 census against the shipped FBX; every panel matched; 8 per octahedron | `python3 …` (`--render` for the sheet) |
| `Tools/Shaders/verify_crystal_morph.py` | compiles and RUNS the shipped HLSL: unstamped is identity, t=0 is the source exactly, t=1 is the target exactly at every phase, monotone, leftovers settle before panels move | `python3 …` |
| `Tools/Shaders/wire_crystal_morph.py --check` | the graph splice is present, acyclic, one feeder per input, slot widths match the HLSL, UV2 not UV1, morph is last | `python3 …` |
| `CrystalMorphMeshBuilderTests` (edit mode) | every panel covers its face exactly; each face claimed once; leftovers collapse to the centre at phase 0; frame 0 is the source vertex-for-vertex; a census mismatch is refused loudly; pentagons work | Unity Test Runner |

All four were run headlessly on this branch, and each gate was negative-controlled (a defect was
injected, the gate was confirmed to fire, the file restored) — a gate that has only ever passed is
indistinguishable from one that cannot fail.

**Not verified: how it looks.** Nothing here has been seen in the editor. Timings
(`stagger 0.35`, `handoffFraction 0.72`, panel phases 0.55–1.0) are authored on
`SquirrelOmniCrystalMorph.asset` and are a starting point for a playtest, not a tuned result.

> ⚠ **`duration` is currently `9` — a 20× INSPECTION value, not a shipping one.** The intended
> figure is `0.45`, the platform's crystal-capture beat (`CrystalCaptureConfigSO`), so a pickup
> reads the same length whichever vessel took it. Every other timing is a FRACTION of `duration`,
> so this one field slows the whole animation and nothing else needs touching. Two things to
> expect while it is slow, both of which are the design running at 1/20 speed rather than faults:
> the ring is **solid and skimmable but invisible for the whole 9 s**, so flying through it gives
> boost off prisms you cannot see; and the morph holds the crystal static for up to 1.5 s first
> while it waits for the ring to be laid.

---

## 7. Cost

One `Mesh` build per crystal collect (~4,300 vertices). The face partition — the expensive half —
is cached per source mesh and computed once per session. Four draw calls while the morph is alive
(one per crystal shell, all on one mesh), for `duration` seconds. The animation itself is a single
stamp; the only per-frame write is the tail cross-dissolve's `_Opacity`, one uniform per shell.

**No collider impact.** The ring's prisms are laid, shielded and registered exactly as before; the
morph adds no collider and changes no gameplay state.

---

## 8. Adding a retirement to another vessel

1. Write a `VesselOmniCrystalRetirementSO` subclass (the Squirrel's is
   `SquirrelCrystalMorphByCrystalEffectSO`).
2. Author its asset and wire it into that vessel's
   `VesselImpactorDataContainerSO.omniCrystalRetirement`.

That one field is also what tells `OmniCrystalImpactor` to skip the husk for that hull, so there
is nothing else to switch off. It is a single slot, not an array, because a crystal is retired
once — two retirements would both claim the crystal's body and draw over each other. Extra
per-vessel flourishes belong in `vesselCrystalEffects` alongside it.

---

## 9. The failure that shipped first, and what now catches it

**`isReadable: 0` on the omni crystal's FBX.** `CrystalMorphMeshBuilder` reads the cage's
vertices on the CPU to bake each face's target, and an IMPORTED mesh without Read/Write does not
return empty vertices — it **throws**. The throw escaped through `BoostRingBuilder.RingLaid`
*after* the eight prisms were already laid, so the visible result was: the ring appears normally,
and the crystal fades out. Which is indistinguishable from "the retirement never ran", "the ring
never arrived", and "the ring was rejected".

Three things came out of it, and the third is the general one:

1. **The fix** — `isReadable: 1` on `OmniCrystalExport1_8-21-25.fbx`, matching
   `ChargeCrystalExport1_7-11-25.fbx`, which this project already reads at runtime for the same
   class of reason. Cost is a CPU copy of a 2,880-vertex mesh.
2. **The guard** — `TryBuild` checks `Mesh.isReadable` before touching a vertex and refuses with
   a diagnosis naming the importer setting. `AnUnreadableSourceMeshIsRefusedByName` pins it.
3. **A listener must never be able to damage conserved mass.** `RingLaid` is now invoked inside a
   try/catch: a *visual* listener that throws must not unwind out of the lay it is watching. The
   exception is reported where it happened rather than three frames away.

And because that animation's one hard dependency (a ring laid by a SIBLING effect) is invisible
to it, **every rejection is now a warning that names what mismatched** — wrong domain, wrong
prism kind, a prism whose shield had not engaged, an unreadable mesh, no ring at all — and the
whole path traces under `CSLogChannel.CrystalMorph` (FrogletTools ▸ Toolbox ▸ Logging). While it
waits for the ring the crystal now holds **static**, not fading, and a ring that arrives during
the give-up fade still takes over.

*A silent fallback is worse than no fallback: it converts every distinct cause into the same
symptom.*

---

## 10. Known limitations

- **`FadeIn` cannot reach a ShepardGraph crystal.** `FadeIn` drives `_opacity` (lowercase) while
  ShepardGraph's property is `_Opacity`, so the bloom-in every crystal is supposed to play is a
  no-op on all four omni shells. Found while wiring the morph's cross-dissolve (which uses the
  correct `_Opacity`); pre-existing, not touched here, and worth its own change.
- **The morph starts at the crystal and the ring is laid at the vessel**, a frame or two later and
  a couple of units further on. At normal Squirrel speeds that is a small extra reach on the
  panels' flight, not a visible seam — but it is the number to look at first if the motion reads
  as stretched.
- **Rotation and scale come from the live crystal, position from the impact.** A respawn moves a
  crystal without resizing or rotating it, so only the position needs to survive the race between
  the collection and respawn RPC chains; if crystals ever gain a per-spawn rotation, that has to
  travel too.
