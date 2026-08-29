# Vessel construction — follow-up brief

Start here for the cleanup that `Docs/VESSEL_CONSTRUCTION.md` scopes. That document is the
evidence; this one is the order of work and the gates between the steps.

**The governing rule for the whole task:**

> **Nothing is deleted until what it carries has been measured and, if valuable, extracted and
> proven in the running game.** Several of the "dead" vessel models carry the only copy of
> something — an armature, a set of animation takes, a real hull morph. "Referenced by nothing"
> means nothing is *using* it, not that it contains nothing.

And its companion, which cost this pass three rounds:

> **Measure, don't infer.** Every number in `VESSEL_CONSTRUCTION.md` came from parsing the FBX or
> the prefab YAML. Re-derive rather than reuse when the source asset changes — a rig swap moves
> every mount on the hull (§4.2 there: the Rhino rig is the shipped hull offset by 1.5545).

---

## Phase 0 — re-establish ground truth (do not skip)

The measurements in `VESSEL_CONSTRUCTION.md` were taken on one day against one branch. Before
acting on any of them:

- Re-run the per-model survey (shapes, shape **magnitude**, bones, takes, referrers) and diff it
  against §1 and §4. Any disagreement means the tree moved and the plan needs re-deriving.
- Confirm guid ownership for every model you are about to touch with `grep -c "^guid: $g"` per
  candidate `.meta` — never `head -1` (§2).

---

## Phase 1 — salvage inventory, before anything is removed

For each of the eight unreferenced models in §4, produce a written answer to: *what is the only
copy of something here?* The measured starting point:

| file | the thing that might be unique | verdict needed |
|---|---|---|
| `dolphin_shapekey_with_animations.fbx` | **a real 4-element hull morph** (10,909 verts moved on mass) + 28-bone armature matching `RiptideAnimation` + 10 takes | **extract — this is the Dolphin's missing morph** |
| `rhino_shapekey_with_animations.fbx` | 12-bone armature matching `RhinoAnimation` + 9 takes. **Element shapes are EMPTY** | keep for the armature; the shapes buy nothing |
| `urchan_shapekey_with_animations.fbx` | 38-bone armature matching `UrchinAnimation` + 11 takes. **Element shapes are EMPTY** | keep for the armature; the shapes buy nothing |
| `Vessel_Placeholder_1.fbx` | 288 animation takes, 110,850 verts | check the takes before removing — 288 is a lot to be nothing |
| `RhinoModel.fbx` | materials that `Vessel_Placeholder_1.fbx.meta` remaps into | resolve the remap first, or removal breaks a `.meta` |
| `Vessel_Placeholder_2.fbx`, `Riptide.fbx`, `Dolphin_split.fbx`, `Hammerhead_split.fbx` | nothing found | safe to retire once Phase 1 is written down |

Do not delete anything in this phase. The deliverable is the inventory.

---

## Phase 2 — the Dolphin rig swap (the one that actually buys a morph)

This is the highest-value item and the only one where the rig carries a real morph. Follow the
procedure `FrogletTools ▸ Vessels ▸ Plan Vessel Rig Swap` prints (report-only, never writes), and
note what the plan tool already flags: gameplay objects that map to no bone go dark when the old
model is disabled, and every legacy part carries its `MeshRenderer` alongside its collider — moving
one onto a bone without retiring its renderer welds the old hull to the new skeleton.

Constraints that are already handled in code and must not be re-solved:

- `VesselAnimation` resolves parts by name, preferring an authored inspector reference. **Leave the
  animation's part fields empty** so they bind to bones.
- Rest poses are captured (`CaptureRestRotations` / `RotatePartFromRest`), so rigged art holds its
  shape. Identity-rest art is unaffected.
- Morph weights are written in `LateUpdate` so the element level stays authoritative over any stray
  animation curve.

What must be re-derived, not carried:

- **Every FX mount on the Dolphin.** Its six jets currently parent to `Engine case *` nodes at a
  mount measured in mesh space. On the rig those become the bones `jetT/jetm/jetB × .l/.r`.
- **Colliders**, which were fitted to the 17 split part meshes by eye.

Acceptance: `FrogletTools ▸ Vessels ▸ Audit Vessel Elemental Morphs` reports the Dolphin, **and**
the hull visibly changes between element level 0 and 10 in play. Both, because of Phase 4.

---

## Phase 3 — Rhino and Urchin: decide honestly, and record the decision

Their rigs' element shapes are **empty placeholders** (§4). So a rig swap on these two buys the
armature, the puppetry and the takes — **not** a morph. Two legitimate outcomes, and the wrong one
is doing it silently:

- **(a) Swap for the puppetry, and leave the morph honestly absent.** The audit must keep reporting
  these two as un-morphed, which requires Phase 4's magnitude check or it will report a false
  green.
- **(b) Get real shapes authored first** (art task) and swap once, with the morph.

Either way, if the Rhino rig is swapped in, re-measure the eight body-jet mounts against the rig —
they are `z − 1.5545` from the shipped numbers, the wings merge into the single skinned mesh so the
two wing jets re-parent to the bones `jet.l`/`jet.r`, and all five `BoxCollider`s need re-fitting
(§4.2).

Grizzly is not in this phase: no `grizzly_shapekey_*` rig exists. It is blocked on art.

---

## Phase 4 — make the audits measure the thing, not its label

These are the checks whose absence let each discrepancy through. All are asset-only, no play mode.

1. **Morph audit measures shape MAGNITUDE.** `Audit Vessel Elemental Morphs` currently reports
   discovered, name-matched shapes. Rhino's and Urchin's rigs would pass it while morphing nothing.
   Report the summed/max vertex delta per shape and fail a shape that moves nothing.
2. **Guid ownership check.** A helper that answers "which file owns this guid" by `^guid:` on the
   candidate's own `.meta`, so the `head -1` trap cannot be re-entered by hand.
3. **Nested-instance reachability check.** For every prefab-instance child of a **plain** Transform,
   assert the instance's stripped Transform appears in that parent's `m_Children` (§3). The
   stripped-parent case is the documented exception.
4. **Duplicate coincident renderer check.** Two `SkinnedMeshRenderer`s drawing the same hull from
   two files (§3.4) should be reported, not discovered by reading YAML by hand.

---

## Phase 5 — retire the vestiges

Only now, and only what Phase 1 cleared. Retire in its own commit, separate from any swap, so a
revert of one is not a revert of the other. Re-run every vessel audit afterwards
(`Audit Vessel Skimmers`, `Audit Vessel Ability Rows`, `Audit Vessel Elemental Morphs`,
`Audit Vessel Tails and Jets`, `Audit Corridor Vessel Radii`) and record the before/after.

---

## Out of scope

The tail/jet contract itself (`Docs/VESSEL_TAIL_AND_JETS.md`) is settled and shipped. This task
does not re-open mount positions except where a rig swap moves the geometry under them.
