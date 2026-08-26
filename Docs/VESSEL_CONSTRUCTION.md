# Vessel construction — how the fleet is actually built, and the three ways that goes wrong silently

**Read before wiring a vessel prefab, swapping a vessel's model, deleting a vessel model, or
placing anything on a hull by measurement.**

This document exists because a single pass over vessel FX (`Docs/VESSEL_TAIL_AND_JETS.md`) turned
up three *different* classes of discrepancy in vessel construction, none of which produced an
error, a warning, or a failing check. Each cost multiple rounds to find. They are unrelated to each
other except in one respect, which is the point of writing them down together:

> **Every one of them is a case where the file says one thing, the running game says another, and
> nothing in between raises its voice.** A vessel prefab is a graph of references — into FBX
> subassets, into nested prefabs, across `.meta` remaps — and the fleet is currently built two
> incompatible ways, so "how do I find X on this vessel" has a different answer per hull.

Nothing here is a proposal. §1–§3 are what was measured; §4 is the inventory that must survive any
cleanup; §5 is the unification target; §6 is the follow-up brief; **§7 is the re-survey that
corrected two of §4's rows** and is the section to read before deleting anything.

---

## 1. The fleet is built two ways, and only one of them can morph

`VesselAnimation` drives two things off the model: **element blend shapes**, discovered by name on
skinned meshes, and **animated parts**, resolved by bone/part name. A hull with no armature and no
shapes answers neither question, so the vessel silently has no elemental hull morph and no
puppetry — the exact failure `CLAUDE.md` records for Dolphin / Urchin / Rhino.

Measured across all twelve vessel prefabs (`MeshFilter` and `SkinnedMeshRenderer` counts, and the
model each one's mesh subasset resolves to):

| vessel | MeshFilter | SkinnedMR | model(s) wired | element shapes |
|---|---|---|---|---|
| Manta | 0 | 2 | `mantis_shapekey_with_animations` **+** `Manta_shapekey_rigged` | 4 (×2 — see §3.4) |
| Falcon | 0 | 2 | same as Manta | 4 (×2) |
| Shrike | 0 | 2 | same as Manta | 4 (×2) |
| Termite | 0 | 2 | same as Manta | 4 (×2) |
| Sparrow | 1 | 1 | `SparrowModel1` (via nested prefab) | 4 |
| Scarab | 2 | 1 | `SparrowModel1` (via nested prefab) | 4 |
| Serpent | 1 | 1 | `SerpentExport` + `serpent` (via nested prefab) | 4 † |
| Squirrel | 0 | 1 | `SquirrelVessel_CosmicShoresTest1` (via nested prefab) | 4 |
| Dolphin ‡ | 2 | 1 | `dolphin_shapekey_with_animations` (via nested prefab) | 4 ‡ |
| **Urchin** | **13** | **0** | `Urchan_Test` ×13 | **0** |
| **Rhino** | **8** | **0** | `Rhino_Test` ×7 | **0** |
| **Grizzly** | **8** | **0** | `Vessel_Wedge_Scene (4)` ×8 | **0** |

- **Skinned family** (top nine): one armature, one skinned hull, element shapes present. Parts are
  bones; a jet or an FX mount parents to a bone and follows it when the model animates.
- **Part-per-mesh family** (bottom three): a static mesh per part, placed by translation. No
  armature, no shapes, so no morph and no puppetry. Parts are GameObjects, and an FX mount parents
  to whichever GameObject the part happens to be.

‡ **The Dolphin CHANGED FAMILIES in this pass** and is the only vessel that ever has. It shipped as
`Dolphin_Test` ×17 static part meshes with no armature and no shapes; **FrogletTools ▸ Vessels ▸
Swap Vessel Rig** re-homed its colliders and FX mounts onto the bones of
`dolphin_shapekey_with_animations` and stripped the legacy art. Post-swap, measured off the shipped
prefab: 2 MeshFilters (the skimmer sphere and the crackle overlay, both built-in meshes and neither
part of the hull), 1 SkinnedMeshRenderer, and four element shapes that MOVE the hull. The row above
is the measured state; the swap's own evidence is §4.3.

† **The Serpent is in BOTH families at once** and is the counter-example to reading this table as a
partition. Its `SkinnedMeshRenderer` draws `SerpentExport.fbx` (one skinned mesh, 27 bones, four
*real* element shapes — Space Δ 2.14, Mass 0.85, Time 2.63, Charge 3.05), and its single
`MeshFilter` draws `serpent.fbx`, a **16-mesh part-per-mesh model with no armature** whose only
blend shape is `serpent_tendril_r` (not an element name, so `VesselAnimation` correctly ignores it).
So the hull morphs and the static half does not. Note `serpent.fbx` is **FBX 7700** — 64-bit records
— which the repo's codec refused to open until this pass; a model that no offline tool can read is a
model whose contents are assumed rather than measured.

The two families are why "where do I mount a jet on this vessel" has no single answer today
(`VESSEL_TAIL_AND_JETS.md` §"How the mount numbers were measured" lists three methods, and which
one applies is a property of the family).

---

## 2. Discrepancy class 1 — guid ownership, and why `head -1` lies

**What happened.** The Rhino's `MeshFilter`s all carry `guid: 4a586f927b9527f469c6d95a0ac32051`.
Resolving it with

```sh
grep -rl "guid: $g" Assets --include=*.meta | head -1
```

returned `Assets/_Models/Vessel Models/Placeholder/Vessel_Placeholder_1.fbx.meta`, which merely
sorts first. Two passes of jet placement were then measured against the placeholder's hull — a
body a fifth the Rhino's height — so the jets sat in empty space above a ship that was never
there, and the *first*, correct measurement was discarded as the mistake.

**Why the false positive is so convincing here.** An FBX's `.meta` can carry an `externalObjects`
material remap pointing into *another* FBX. (Both placeholders were retired in Phase 5; the
mechanism is what matters and it is unchanged, so the example is kept as written. `git show
HEAD~1 -- 'Assets/_Models/Vessel Models/Placeholder/'` if you want to look at them.)
`Vessel_Placeholder_1.fbx.meta` remaps its materials
into `Rhino_Test.fbx` (`4a58…`) and `RhinoModel.fbx` (`d342…`) — the placeholder borrows the
Rhino's materials. So a model file's `.meta` containing a model guid is completely ordinary and
looks exactly like ownership.

**The rule.**

> **Exactly one `.meta` OWNS a guid — the file whose own top-level `guid:` line carries it. Every
> other hit is something referencing it.** Resolve with `grep -c "^guid: $g"` per candidate and
> take the file that returns 1. Never `head -1`.

**And cross-check the answer against something the prefab itself authored.** Here the Rhino body's
`BoxCollider` reproduces `Rhino_Test.fbx`'s `fusalage` extents `(5.6182, 15.3091, 16.8157)` and
centre `(0, −1.1999, −1.9744)` to four decimals. One independent agreement turns a resolution into
a fact.

---

## 3. Discrepancy class 2 — a nested prefab instance is reachable two ways

**What happened.** Eight `VesselJet` instances were written into `Rhino.prefab` with a correct
`m_TransformParent` in each instance's own modification block, and were **absent from the parent
Transform's `m_Children`**. Every tail and jet in the fleet that demonstrably renders carries both.

**Why the wrong generalisation was reached.** The Squirrel's four jets carry no `m_Children` entry
and work. They *cannot* carry one: their parent is itself a **stripped** Transform inside the
nested model prefab, and a stripped document is a reference stub with no children list to write
into. Generalising "no entry needed" from the single case that structurally cannot have one is
what produced the omission.

**The rule.**

> A nested prefab instance's parenting lives in **two** places, and which ones apply depends on the
> parent: `m_TransformParent` in the instance's modification block **always**, plus an entry in the
> parent Transform's `m_Children` **iff that parent is a plain (non-stripped) Transform**.

Census across all twelve vessel prefabs, re-measured at ship time: 45 tail/jet instances have a
plain-Transform parent and all 45 are listed in it; the Squirrel's four are the only
stripped-parent case; zero are unreachable. That third number is the invariant — the first two
move whenever a vessel gains or loses FX, so re-measure rather than trusting them.

### 3.4 The Manta family draws two coincident hulls — open question, not yet a verdict

Measured, not inferred: Manta, Falcon, Shrike and Termite each carry **two** `SkinnedMeshRenderer`s,
both on GameObjects named `manta`, both `m_IsActive: 1`, both `m_Enabled: 1`, drawing the same
3,351-vertex hull from two different files:

| file | unit scale | node | shape deltas (max) | takes |
|---|---|---|---|---|
| `Manta_shapekey_rigged.fbx` | raw bounds ±197.6 | `S=1, R=0` | Mass 55.30 · charge 33.64 · space 81.97 · time 71.79 | 0 |
| `mantis_shapekey_with_animations.fbx` | raw bounds ±1.98 | `S=100, R=−90x` | Mass 0.553 · charge 0.336 · space 0.820 · time 0.718 | 9 |

They are the **same export at 100× different unit scale**, with the node scale compensating, so
after import both land at the same world size and their morphs have the same amplitude — this is
not a tearing bug. But it is a duplicate skinned draw of the same hull on four vessels, and the
morph audit sees 8 element shapes where the contract says 4. Whether the second renderer is
deliberate (shadow/collision proxy) or a merge artefact has **not** been established; do not delete
either until it is. This is the same shape as the coincident-nucleus rule in `CLAUDE.md`
("never hand-place a membrane/nucleus/cytoplasm in a scene") reached independently on the vessel
side.

---

## 4. Discrepancy class 3 — model vestiges, and what is actually inside them

Eight vessel models are referenced by **nothing** in `Assets` (no prefab, scene, asset or animator
controller). **Six of them, plus a duplicate Manta controller, were retired in Phase 5** — the rows
are kept because what each one was measured to contain is the evidence for that decision. They are not all the same kind of dead, and the difference is the whole point of this
section: **two of them are the missing rigs for vessels that cannot morph, and one of those rigs
carries a real morph while the other two carry empty placeholders.**

Measured per file — base mesh, armature, and how far each element shape actually moves the hull:

| file | verts | bones | takes | element shapes | **do the shapes move anything?** | refs |
|---|---|---|---|---|---|---|
| `dolphin_shapekey_with_animations.fbx` | 12,595 | 28 | 10 | 4 | **YES** — mass moves 10,909 verts (max Δ 1.173), time 7,473 (1.317), charge 2,339 (0.420), space 1,217 (0.306) | **0** |
| `rhino_shapekey_with_animations.fbx` | 8,549 | 12 | 9 | 4 | **NO — all four are empty** (1 vertex, Δ = 0.0000) | **0** |
| `urchan_shapekey_with_animations.fbx` | 6,080 | 38 | 11 | 4 | **NO — all four are empty** (1 vertex, Δ = 0.0000) | **LIVE** — 7 clips × 4 controllers → 9 vessels (§7.1) |
| `SparrowModel4.fbx` | 13,426 | 65 | 93 | 4 | **YES** (same deltas as `SparrowModel1`) | **LIVE** — `Missile Launch` clips → Sparrow + Scarab (§7.1) |
| `Vessel_Placeholder_1.fbx` | 110,850 | 0 | 288 | 0 | — | 0 |
| `Vessel_Placeholder_2.fbx` | 68,995 | 0 | 0 | 0 | — | 0 |
| `RhinoModel.fbx` | 8,087 | 0 | 0 | 0 | — | 0 (but **both** placeholder `.meta`s remap materials into it) |
| `Riptide.fbx` | 1,326 | 0 | 0 | 0 | — | 0 |
| `Dolphin_split.fbx` | 137 | 0 | 0 | 0 | — | 0 |
| `Hammerhead_split.fbx` | 116 | 0 | 0 | 0 | — | 0 |

The `verts` column is the **base mesh** vertex count. An earlier revision summed the mesh *and*
every blend-shape delta array, which is why it read 39,613 / 8,553 / 6,084 — a shape stores only the
vertices it moves, so summing them counts moved vertices a second time. Both definitions are
reproducible; only one describes the hull.

**The finding that changes the plan: a labelled shape is not a shape.** `VesselAnimation`
discovers element shapes **by name**, and `FrogletTools ▸ Vessels ▸ Audit Vessel Elemental Morphs`
reports what it discovers. Swap the Rhino or Urchin rig in and both would report **4 labelled
shapes — a green audit — while the hull morphs by exactly nothing.** That is worse than the
current honest zero. Any morph audit or acceptance check must measure **shape magnitude**, not
shape presence.

### 4.1 The three rigs' bones already match the code

The part names `RiptideAnimation`, `RhinoAnimation` and `UrchinAnimation` resolve by are present in
the corresponding rigs, so the **code half of a rig swap is already done**:

| rig | bones the animation script names | present |
|---|---|---|
| dolphin | `jetT/jetm/jetB × .l/.r`, `jaw.u`, `jaw.b` | yes (28 bones) |
| rhino | `wing1.*`, `wing2.*`, `jet.*` | yes (12 bones) |
| urchan | `gunM.*`, `jetT.*`, `jetB.*` | yes (38 bones) |

### 4.2 The Rhino rig is the shipped hull, provably — and it is 1.5545 units aft

Not a similar model: the **same** one. `rhino_shapekey_with_animations.fbx`'s single skinned mesh
`Circle.004` (8,549 verts) is the whole Rhino merged — fuselage **and** wings — and every lathe ring
of the shipped `fusalage` reappears in it at identical radius and identical vertex count, offset by
a constant:

```
shipped fusalage            rig Circle.004              dz
z -10.382  n=64 r[0.766,0.882]   z -8.828  n=64 r[0.766,0.882]   +1.5540
z  -9.067  n=64 r[0.822,0.944]   z -7.512  n=64 r[0.822,0.944]   +1.5550
z  -3.180  n=64 r[0.452,0.574]   z -1.626  n=64 r[0.452,0.574]   +1.5540
```

Consequences for a rig swap, and they are not optional:

- **Every measured mount on the Rhino moves.** The eight body-jet mounts in
  `VESSEL_TAIL_AND_JETS.md` are in `Rhino_Test` space; on the rig they are `z − 1.5545`. Re-measure
  against the rig, do not translate by hand and hope.
- **The wings stop being separate GameObjects.** `Rhino_Test` splits `Wing front/back L/R` and
  `engine L/R` into six meshes; the rig merges all of it into one skinned mesh, and the wing pods
  become the lathe axes `(±5.365, +0.464)`. The two wing jets currently parent to `engine
  left`/`engine right` at local origin — those nodes will not exist. They re-parent to the bones
  `jet.l`/`jet.r`.
- **Colliders were fitted to the split hulls.** Five `BoxCollider`s were authored against
  `Rhino_Test`'s parts. They must be re-fitted, not carried across.

### 4.3 The Dolphin rig is the shipped hull too — at the same place, with no offset

The Rhino answer (§4.2) is not the general one. `dolphin_shapekey_with_animations.fbx` is also the
**same** ship, but its correspondence is *exact*: transform both files' geometry into world space
and the bounding boxes agree on all six faces to three decimals —

```
Dolphin_Test  n=12,583  bbox (-264.363, -56.747, -247.074) .. (264.363, 64.173, 282.702)
rig           n=12,595  bbox (-264.363, -56.747, -247.074) .. (264.363, 64.173, 282.702)
```

— and a nearest-neighbour match runs **8,311 of 12,583 shipped vertices to within 5.5 × 10⁻⁵** of a
rig vertex, in a hull 758 units across (0.000007%). The rig's `dolohin` node carries `S = 100,
R = −90x` against mesh vertices at 1/100 scale; both files import at the same 0.01 file scale, so
they land at the same world size. **There is no offset to correct on the Dolphin.** Do not
generalise the Rhino's 1.5545 to it, and do not generalise the Dolphin's zero to anything else —
each rig has to be measured.

**What the rig has that the shipped hull does not: six exhaust nozzles.** 4,284 rig vertices have no
counterpart in `Dolphin_Test`, and they cluster into six symmetric groups at the stern
(`x ±40…±147, y −15…60, z −229…−28`) — one per engine pod. The cause is in the shipped prefab:
`Dolphin_Test`'s six `Engine Left/Right.N` inner meshes sit at **`localScale 0.01`** under their
`Engine case *` parents. Their mesh is 9.5 × 9.5 × 18.3 file units, so after the 0.01 file scale
*and* the 0.01 local scale they are **0.00095 Unity units** in a 5.3-unit ship — 1/5,600 of its
length. They are not small; they are not drawn.

Rendering both stems settles it by eye rather than by statistics: the shipped pods are smooth
sealed cones, and the rig's have open, recessed exhaust bells cut into their rear faces. So the
Dolphin rig swap does not only buy the morph and the puppetry — **it restores the six nozzle mouths
the six jets are mounted at** (`VESSEL_TAIL_AND_JETS.md` §4 measures each jet to "each pod's
measured exhaust mouth"; on the rig that mouth is real geometry rather than an inferred plane).

### 4.4 A rig's REST POSE is not the shipped hull's pose — measure the silhouette, not just the mounts

§4.2 proves the Rhino's *fuselage* is a rigid +1.5545 z translation, and re-measuring it confirms
that exactly: **all 29 of the shipped fuselage's lathe rings** match the rig at `+1.5550` (this
pass's z-quantisation is 0.005, so the two agree). But that is the fuselage alone. Whole-model
bounds do **not** translate rigidly:

| | shipped | rig | ratio |
|---|---|---|---|
| Rhino x half-span | 5.796 | 7.998 | **1.380** |
| Rhino y span | 15.309 | 15.309 | 1.000 |
| Rhino z span | 16.815 | 20.409 | 1.214 |
| Urchin x/y/z span | 0.432 / 0.406 / 0.414 | 0.910 / 0.854 / 0.873 | **2.107 / 2.105 / 2.105** |

Two different phenomena, and telling them apart matters:

- The **Rhino** ratios are non-uniform with `y` exactly 1.000 — that is a **POSE** difference. The
  rig's bind pose has the wings rotated further out than the pose `Rhino_Test` was authored in.
- The **Urchin** ratios are uniform to four digits — that is a **SCALE** difference. Rescaling the
  rig by `1/2.105` matches 2,500 of 2,500 sampled vertices at a median residual of 0.0158 in a
  43.2-unit hull (0.04%). The Urchin rig is the shipped hull at **2.105×**.

> **A rig swap changes the ship's resting silhouette, not only where its mounts are.** The Urchin's
> is the sharper case: it flies a 6.67-unit camera against a ~0.43-unit hull
> (`VESSEL_TAIL_AND_JETS.md` §6 calls it "the extreme case"), and 2.105× takes that hull to ~0.91.
> Camera distance, collider volumes, `widthScale`, and the occlusion corridor's measured hull radius
> are all functions of that number.

### 4.5 The offline mirror and the in-editor audit disagree by a constant — and both are right

Running **FrogletTools ▸ Vessels ▸ Audit Vessel Elemental Morphs** on the swapped Dolphin and
`measure_vessel_models.py` on the same FBX gives two different sets of numbers for the same four
shapes:

| shape | in-editor | offline | ratio |
|---|---|---|---|
| Mass | 12.056% | 15.471% | 1.283 |
| Charge | 4.314% | 5.535% | 1.283 |
| Space | 3.140% | 4.030% | 1.283 |
| Time | 13.538% | 17.372% | 1.283 |

**The ratio is identical to four digits across all four shapes, so the disagreement is in the
DENOMINATOR, not in the deltas.** Both tools report `farthest vertex delta / mesh diagonal`; they
agree on the numerator and differ on what "the mesh's diagonal" means. Unity's `mesh.bounds` for a
**skinned** mesh is the culling volume expressed in ROOT-BONE space, not the raw vertex AABB the
offline reader computes — the same distinction `CLAUDE.md` already records for the prism occlusion
corridor, where measuring a skinned hull off `sharedMesh.bounds` overstated the Sparrow by its
whole armature factor.

Three consequences:

- **Neither number is wrong, and the verdict is unaffected.** The threshold is 0.1% and the real
  shapes are 2.46–17.94% by either measure, while an inert one is 0.0000% by both — the gap the
  threshold sits in is four orders of magnitude wide, so no plausible denominator can move a shape
  across it.
- **Do not "reconcile" them by changing either tool.** The in-editor audit must measure what Unity
  measures, because that is what the runtime morphs against; the offline mirror must stay readable
  without Unity, which is the whole reason it exists.
- **Compare a magnitude only against numbers from the SAME tool.** A per-model constant means a
  cross-tool comparison silently rescales, and the constant differs per model (it is that rig's
  root-bone factor).

### 4.6 A rig swap moves GEOMETRY. Two other things decide whether the ship works.

The Dolphin swap passed every structural check — bones, colliders, renderers, morph magnitude,
reachability, dangling references — and the vessel was still unusable when flown. Both defects were
in things the swap did not touch, which is exactly why nothing caught them.

**MATERIALS are authoring, not geometry.** A model ships its DCC materials unless its `.meta`
REMAPS them onto the fleet's three roles through `externalObjects`. The Squirrel and Sparrow do;
the Dolphin rig's map was empty, so `accent.001` — the material that should carry the DOMAIN
colour — rendered as its Blender authoring: `EmissiveColor (1.0, 0.395, 0.0)` at `EmissiveFactor
10`. Saturated orange, emissive 10×, on the one surface that is supposed to say which team you
are.

**And submesh ORDER is not a convention.** Measured across the fleet:

| model | submesh order | slot 1 is |
|---|---|---|
| Sparrow | Body, Domain, Engine, Window | Domain ✔ |
| Manta family (serialized) | Body, Window, Domain, ? | **Window** |
| Dolphin rig | accent, BASE, windsheild, N | **BASE — the body** |

`VesselCustomization._domainMaterialSlot` defaults to 1 and its tooltip calls that "the platform
contract". It is not a contract, it is a coincidence that holds for models which happen to emit
Domain second. On this rig it painted the team colour over the whole dark body — the exact outcome
the same tooltip forbids ("erases the two-tone read"). **Use `_domainReplacesMaterials`**, which
resolves the slot by material IDENTITY and cannot be wrong about order.

> **A rig swap is three jobs, and the tool only ever did one.** Move the geometry; remap the
> materials; make the animation rig-safe. Passing the first is not evidence about the other two —
> and both of those failures render, so they are invisible to every asset-level check that exists.

**REST FRAMES are the third job's real content, and rotation is the half that bites.**
`Quaternion.Euler(pitch, yaw, roll)` assigned to `localRotation` turns about the **parent's** axes.
On part-per-mesh art every animated part hangs off the model root, so those are the ship's axes and
pitch means pitch. A bone's parent is another BONE, pointing wherever the skeleton points — so the
identical call rolls when it meant to pitch, and pitches backwards. Measured against this rig's own
bone rest rotations, for a commanded **pitch**:

| bone | axis it actually turned about | reads as |
|---|---|---|
| `jetT.l` | `(−0.541, 0.840, −0.043)` | mostly YAW, pitch inverted |
| `jetB.r` | `(0.516, −0.851, −0.103)` | mostly yaw, the other way |
| `winghold.l` | `(−1.000, 0, 0)` | **the ship's pitch axis, exactly negated** |
| `fuse`, `wing.l` | `(1, 0, 0)` | correct — these rest ship-aligned |

`winghold.l` is the whole "pitch is inverted" report in one row: it rests at `(77.08, 0, −180)`, and
that −180° roll flips X, so the wings pitched backwards while the engines yawed.

**And the rest pose must be re-anchored, or a drift throws the part away.** These parts are
re-parented onto a drift handle in flight and `parent =` preserves the world pose — so a rest
rotation captured under a BONE, replayed under the handle, lands somewhere the part never was.
Measured: **164.50°** for the wings and **152.38°** for the engines, on drift entry, with no pilot
input at all. That is the "wings fold up when drifting" report.

`RotatePartFromRestInFrame` fixes both halves together — conjugate the turn into the frame the
animation MEANT (the vessel, or the drift handle while drifting), anchor the rest through the part's
HOME parent — and is a SEPARATE method from `RotatePartFromRest` on purpose, because that one is
shared with the Scarab, whose animation was authored and play-tested against the current behaviour.
Re-proved offline against the rig's measured bones by
`Tools/Build/verify_vessel_rig_puppetry_frames.py`, which also asserts the two invariants that keep
it honest: a ship-aligned part is bit-identical under both formulas, and entering a drift with no
input moves nothing.

**Two more the third flight found, both of which the frame fix made VISIBLE rather than caused.**

*Roll reads backwards — and it is NOT this layer.* Reported as inverted, negated inside
`RiptideAnimation`, then reported **still** swapped: a pure sign flip that does not change the
symptom is telling you the lever is somewhere else. It is the **hull**. `VesselTransformer.Roll()`
banks the ship about `transform.forward` from `InputStatus.YDiff`, is byte-identical to
`bleeding-edge`, and is shared by **six vessels** (Manta, Squirrel, Falcon, Rhino, Shrike, Dolphin).
Negating in the animation only made the wings disagree with the hull they are bolted to, so it was
reverted; the puppetry passes roll through untouched and follows the flight model wherever it goes.

> **When a sign flip does not change the symptom, stop flipping signs.** A pure negation is one of
> the few edits whose effect is certain, so a symptom that survives it is evidence about WHERE the
> defect is, not about which way it points. Two flights were spent on this before the question was
> put to the pilot, who answered it in one sentence: *it's the whole ship, not the wings.*

Changing it is a flight-model decision across six vessels, deliberately left out of a
vessel-construction pass.

*The WINGS, separately, bank against the hull* — and this one is not derivable. The conjugation
turns every part about the ship's `+Z`, so the wings' delta is provably identical in axis and sign
to the chassis's (measured: both `25°/75° about (0,0,1)`), yet on screen they read opposite. The
authored sign was chosen for legacy art where the wings hung off the model root at identity; on the
rig they hang off `winghold` bones carrying a −180° roll. Rather than guess a fourth sign, the wings'
roll contribution is now a **signed, authorable response** (`wingRollResponse`, default −1): +1 banks
them with the hull, −1 against, 0 stops them responding to roll. It is legitimate authoring rather
than a flag — how hard the wings bank relative to the hull is a feel parameter — and it makes the
question answerable in the editor in seconds instead of a round-trip per guess.

> **When the math and the screen disagree, and the math is verified, the answer is a parameter.**
> Three sign guesses cost three flights. A number the pilot can turn costs none.

*A COURSE FRAME MUST NOT TWIST, and `LookRotation` cannot give you one.* Built as
`LookRotation(Course, hull.up)`, the frame's roll is pinned to whatever up it is handed — so as the
hull aims away from Course that up swings and drags the frame around the Course axis. The parts then
rotate with **zero pilot input**, and it reads exactly as it looks: like they are holding their up
vector against the camera instead of letting the camera swing around them. Measured over a 0→50°
aim sweep with Course fixed:

| frame construction | twist accumulated |
|---|---|
| `LookRotation(Course, hull.up)` | **19.77°** |
| rebuild from the hull's current nose | 7.03° |
| rotation-minimizing (shipped) | **0.000024°** |

The middle row is the trap: it is the obvious fix, it is three times better, and it is still wrong —
the shortest arc from a *moving* nose changes as the nose moves, which leaks roll back in. The frame
is instead seeded from the hull at drift entry and thereafter turned by the shortest arc from **its
own previous forward** onto Course. Every step is a pure swing about an axis perpendicular to both,
so it adds no twist by construction.

> **"Point this at that" is not enough to define a frame.** A direction leaves one degree of freedom
> — the roll about it — and every look-at helper silently fills it in from an up-vector that is
> usually moving. When something should hold still while the world turns around it, carry the frame
> forward from its own last orientation instead of rebuilding it from a reference that moves.

*And while drifting the appendages go QUIET.* Holding Course is the whole of their job during a
drift; only the fuselage and jaws respond to the stick. Leaving the wings and engines responsive
reads as the ship still flying while it is supposed to be sliding.

*A DRIFT SPLITS THE SHIP IN TWO — and getting that backwards cost a flight.* The fuselage and jaws
turn to **aim** wherever the pilot points, while the wings and engines stay lined up with **Course**,
the direction the ship is actually travelling, so the hull reads as slewing across its own path. The
appendages are the instrument that tells everyone which way the Dolphin is really going. Read
"the wings move forward and the jets backward so the fuselage and jaws can aim without clipping" as
*only* a clearance statement and you delete the signalling — which is what happened here: the
re-parenting onto a Course-aligned handle looked like dead machinery and was removed, when it was
the feature. It is now expressed as a **frame** rather than a re-parent
(`RotatePartFromRestInFrame` carries the part's vessel-relative rest pose onto whatever frame it is
handed), so nothing moves in the hierarchy and a rig's bone rest poses survive it.

> **Machinery that looks inert may be the feature, seen through a bug.** The re-parenting did
> nothing observable only because the frames were already wrong; "it has no effect, delete it" was
> a conclusion drawn from a broken build.

*And the drift had also lost half its purpose.* A drifting Dolphin swings its hull to aim while it keeps
sliding, so **the wings slide forward and the engines slide back** to open a gap the fuselage and
jaws can turn through without clipping. The engines' "backward" and "default" offsets were the
**same vector**, so they never moved — the hull shipped with half its clearance missing, and
translating the constants faithfully preserved that. Worse, both wings and engines were re-parented
onto a handle aimed along `Course`, so they swung *with* the aim instead of clearing it. The frame
is now always the vessel and the drift is purely translational; the re-parenting machinery
(`CaptureHomeParents` / `DriftParts` / `ReparentToDrift` / `ReparentHome`, 45 lines) is deleted,
because once the frame was fixed it did nothing at all.

> **A degenerate constant is not a design.** `backwardThrusterPosition = defaultThrusterPosition`
> reads as deliberate and is indistinguishable from a value nobody finished. Translating it
> faithfully carried a missing feature forward into new code. When a pair of tuning constants are
> equal, that is a question to ask, not a fact to preserve.

The clearance is now authored (`driftWingForward`, `driftJetBackward`) rather than constant, because
how much room the jaws need is a feel question.

**REST POSITIONS are the other half of §4.4.** That section says a rig's rest pose is not the
shipped pose, and the codebase had applied the lesson to ROTATION only: `RotatePartFromRest` existed,
its positional sibling did not, so `RiptideAnimation` still wrote `localPosition` absolutely. An
absolute local position assumes a part hangs off the model root. On a rig it is wrong twice — the
value is relative to the PARENT BONE, and in Blender's convention every bone rests at
`(0, boneLength, 0)` — so a constant meant as "just behind the hull" flung each of six engines
1.7 units along a different rotated axis. `CaptureRestPositions` / `MovePartFromRest` complete the
pair, with a detail worth keeping: the ANCHOR resolves through the part's HOME parent and the
OFFSET through its CURRENT one, because these parts are re-parented onto a drift handle in flight
and each half would be wrong under the other's parent.

Translating those constants also exposed dead tuning: the "default" and "backward" thruster
positions were the same vector, so the thrusters never had a positional animation at all. The one
real positional effect in the Dolphin's whole puppetry is the wings sliding forward on a drift.

---

## 7. Phase 0 re-survey — what moved, and the two rows that were wrong about liveness

Re-run before any of this document's cleanup was acted on, per
`VESSEL_CONSTRUCTION_FOLLOWUP.md` § Phase 0. Reproduce with:

```sh
python3 Tools/Build/measure_vessel_models.py      # shapes + MAGNITUDE, bones, takes, referrers
python3 Tools/Build/measure_vessel_prefabs.py     # renderers, guid ownership, §3 reachability, §3.4 dupes
```

**Confirmed unchanged**, and these are the load-bearing ones: §1's twelve-row renderer table
exactly; §3's invariant (**zero** unreachable instances across all twelve prefabs); §3.4's duplicate
coincident Manta-family renderers (four vessels, both enabled and active, the same hull from two
files at 100× scale); §4's central finding (dolphin shapes real, rhino and urchan shapes empty at
one indexed vertex and Δ = 0.0000); §2's guid ownership (`Rhino_Test.fbx` owns `4a58…`;
`Vessel_Placeholder_1.fbx.meta` merely references it).

### 7.1 "Referenced by nothing" was measured against prefabs and scenes — clips slipped through

The rule this section exists to state:

> **A model can have zero prefab references and still be load-bearing, because an
> `AnimatorController` references CLIPS by the model's guid.** Deleting it does not break a
> reference you can see in a prefab; it empties an animation state in a controller that a shipped
> vessel is using.

Two files are in exactly that position, and one of them this document never listed at all:

| model | prefab refs | what actually uses it |
|---|---|---|
| `urchan_shapekey_with_animations.fbx` | **0** | **7 clips** in `_Models/Animations/MantaAnimatorController` (Manta, Termite, Falcon, Urchin, Shrike), `SerpentAnimController` (Serpent), `SparrowAnimatorController` (Sparrow, Scarab), `SquirrelAnimatorController 1` (Squirrel) — **nine vessels** |
| `SparrowModel4.fbx` | **0** | the `Missile Launch` states in `SparrowAnimatorController` (Sparrow, Scarab). 93 takes against `SparrowModel1`'s 13 |

`mantis_shapekey_with_animations.fbx` is in the same class but was never in doubt — it is also
prefab-wired on four vessels.

**And the mirror image: a controller that nothing uses.** There are two files named
`MantaAnimatorController.controller`. The live one is **`Assets/_Models/Animations/`** (five vessels
reference it); **`Assets/_Animations/MantaAnimatorController.controller` is referenced by nothing**
and is the only reason four of the five "controller" hits in the old §4 count existed. A reference
count is not a liveness measurement until each referrer is itself resolved.

### 7.2 Smaller corrections

- `RhinoModel.fbx` is material-remapped by **both** placeholder `.meta`s, not just
  `Vessel_Placeholder_1`'s. Removing it breaks two files, not one.
- `RhinoModel.fbx` is a **predecessor** of `Rhino_Test.fbx`, not an unrelated model: `Circle.003`
  (5,135), both `Cylinder`s (864) and both `Circle.015/016` (367) are vertex-identical; only the
  front wings differ (245 → 476). It is the same ship before the wings were subdivided.
- `Vessel_Wedge_Scene (4).fbx` (the Grizzly's model) is referenced by **three** prefabs —
  `Grizzly`, `TimeDandruff` and `ExplodableProjectile` — plus a `.meta` remap from
  `TimeCrystalExport.fbx`. It is shared with the environment and projectile layers and is not a
  vessel-only asset.
- `serpent.fbx` is FBX **7700**; `Tools/Build/fbx_binary.py` now reads 64-bit records (writing them
  is still refused, loudly, rather than emitting a 32-bit body under a 64-bit version stamp).

---

## 5. The unification target

One way to build a vessel, so that every question about a hull has one answer:

1. **One model per vessel, skinned, with an armature and four *non-empty* element shapes.** The
   part-per-mesh family is the exception to retire, not a second supported style.
2. **Parts are bones, resolved by name.** `VesselAnimation.ResolvePart` already prefers an authored
   inspector reference and falls back to a name lookup, which is what makes an art swap cheap —
   stale references come back null and the rig's bones bind themselves.
3. **FX mounts parent to the part they belong to**, so they follow it when it animates
   (`VESSEL_TAIL_AND_JETS.md`). A mount measured off static hull geometry is a fallback for hulls
   with no bone for that feature — the Rhino's eight body nozzles are legitimately in this class;
   its wing pods are not.
4. **Every audit measures the thing, not its label** (§4).

---

## 6. Follow-up brief

The work this document scopes is *not* done. See `Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md` for the
brief a fresh session should start from — it carries the salvage-before-delete order, the
per-vessel state, and the checks that have to exist before any model is removed.
