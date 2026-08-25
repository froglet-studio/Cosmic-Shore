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
cleanup; §5 is the unification target; §6 is the follow-up brief.

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
| Serpent | 1 | 1 | `SerpentExport` + `serpent` (via nested prefab) | 4 |
| Squirrel | 0 | 1 | `SquirrelVessel_CosmicShoresTest1` (via nested prefab) | 4 |
| **Dolphin** | **19** | **0** | `Dolphin_Test` ×17 | **0** |
| **Urchin** | **13** | **0** | `Urchan_Test` ×13 | **0** |
| **Rhino** | **8** | **0** | `Rhino_Test` ×7 | **0** |
| **Grizzly** | **8** | **0** | `Vessel_Wedge_Scene (4)` ×8 | **0** |

- **Skinned family** (top eight): one armature, one skinned hull, element shapes present. Parts are
  bones; a jet or an FX mount parents to a bone and follows it when the model animates.
- **Part-per-mesh family** (bottom four): a static mesh per part, placed by translation. No
  armature, no shapes, so no morph and no puppetry. Parts are GameObjects, and an FX mount parents
  to whichever GameObject the part happens to be.

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
material remap pointing into *another* FBX. `Vessel_Placeholder_1.fbx.meta` remaps its materials
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

Census across all twelve vessel prefabs is currently clean: 36 tail/jet instances with a
plain-Transform parent are all listed there; the Squirrel's four are the only stripped-parent case.

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
controller). They are not all the same kind of dead, and the difference is the whole point of this
section: **two of them are the missing rigs for vessels that cannot morph, and one of those rigs
carries a real morph while the other two carry empty placeholders.**

Measured per file — base mesh, armature, and how far each element shape actually moves the hull:

| file | verts | bones | takes | element shapes | **do the shapes move anything?** | refs |
|---|---|---|---|---|---|---|
| `dolphin_shapekey_with_animations.fbx` | 39,613 | 28 | 10 | 4 | **YES** — mass moves 10,909 verts (max Δ 1.17), time 9,272 (1.32), charge 4,187, space 2,650 | **0** |
| `rhino_shapekey_with_animations.fbx` | 8,553 | 12 | 9 | 4 | **NO — all four are empty** (1 vertex, Δ = 0.0000) | **0** |
| `urchan_shapekey_with_animations.fbx` | 6,084 | 38 | 11 | 4 | **NO — all four are empty** (1 vertex, Δ = 0.0000) | 5 (animator controllers only, no prefab) |
| `Vessel_Placeholder_1.fbx` | 110,850 | 0 | 288 | 0 | — | 0 |
| `Vessel_Placeholder_2.fbx` | 68,995 | 0 | 0 | 0 | — | 0 |
| `RhinoModel.fbx` | 8,087 | 0 | 0 | 0 | — | 0 (but `Vessel_Placeholder_1.fbx.meta` remaps materials into it) |
| `Riptide.fbx` | 1,326 | 0 | 0 | 0 | — | 0 |
| `Dolphin_split.fbx` | 137 | 0 | 0 | 0 | — | 0 |
| `Hammerhead_split.fbx` | 116 | 0 | 0 | 0 | — | 0 |

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
