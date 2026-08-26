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

## Phase 0 — re-establish ground truth  ✅ DONE

Re-run with `Tools/Build/measure_vessel_models.py` and `measure_vessel_prefabs.py`; the diff is
`VESSEL_CONSTRUCTION.md` **§7**. §1, §3, §3.4 and §4's central finding all reproduced exactly. Two
things re-derived the plan and are folded into Phase 1 and Phase 5 below:

- **`urchan_shapekey_with_animations.fbx` and `SparrowModel4.fbx` are LIVE**, not vestiges — a model
  with zero prefab references can still be supplying animation CLIPS to shipped vessels (§7.1).
- **A rig's rest pose is not the shipped pose** (§4.4): the Rhino's wings sit 1.38× wider in the
  rig's bind pose, and the Urchin rig is a uniform **2.105× scale** of the shipped hull. Phase 3's
  decision has to price that in.

Whenever this is re-run: confirm guid ownership with `grep -c "^guid: $g"` per candidate `.meta`,
never `head -1` (§2) — `measure_vessel_prefabs.py` does this and reports an ambiguous answer rather
than picking one.

---

## Phase 1 — salvage inventory  ✅ DONE (nothing deleted)

**The question each row answers: *what is the only copy of something here?*** Measured with
`Tools/Build/measure_vessel_models.py`; liveness resolved per §7.1 of `VESSEL_CONSTRUCTION.md` —
prefab references AND animator-clip references, with each referring controller itself resolved to
the prefabs that use it.

### 1a. NOT vestiges — these are live and must not be touched

| file | why it is live |
|---|---|
| `urchan_shapekey_with_animations.fbx` | **7 animation clips on 9 vessels.** Zero prefab refs, but `_Models/Animations/MantaAnimatorController` (Manta, Termite, Falcon, Urchin, Shrike), `SerpentAnimController`, `SparrowAnimatorController` (Sparrow, Scarab) and `SquirrelAnimatorController 1` all pull clips out of it. Also the only copy of the 38-bone Urchin armature `UrchinAnimation` names. Its four element shapes are empty (§4). |
| `SparrowModel4.fbx` | The `Missile Launch` states in the live `SparrowAnimatorController` (Sparrow + Scarab). 93 takes against `SparrowModel1`'s 13 — the only copy of 80 of them. Same hull, same 65 bones, same four real element shapes as `SparrowModel1`. |
| `Vessel_Wedge_Scene (4).fbx` | The Grizzly's model, and also wired into `TimeDandruff.prefab` and `ExplodableProjectile.prefab`, plus a `.meta` remap from `TimeCrystalExport.fbx`. Not a vessel-only asset. |

### 1b. Carries something unique — keep, and say what for

| file | the only copy of | verdict |
|---|---|---|
| `dolphin_shapekey_with_animations.fbx` | a **real** 4-element hull morph (mass moves 10,909 verts, Δ 1.173; time 7,473, Δ 1.317; charge 2,339; space 1,217) · the 28-bone armature `RiptideAnimation` names · 10 flight takes · **and the six engine exhaust nozzles the shipped hull scales to 0.00095 units** (§4.3) | **extract — this is Phase 2** |
| `rhino_shapekey_with_animations.fbx` | the 12-bone armature `RhinoAnimation` names · 9 flight takes. Element shapes are **empty** (1 indexed vertex, Δ = 0.0000) | keep for the armature + takes; the shapes buy nothing (§Phase 3) |
| `RhinoModel.fbx` | nothing of its own — it is `Rhino_Test.fbx` **before the front wings were subdivided** (`Circle.003`, both `Cylinder`s and both `Circle.015/016` are vertex-identical; wings 245 → 476). But **both** placeholder `.meta`s remap materials into it, so removing it breaks two files | keep until the placeholders go; then it goes with them |

### 1c. Nothing unique found — cleared for Phase 5

Each was checked for shapes, bones, takes, referrers, and for geometry not present elsewhere.

| file | what it is | why it carries nothing |
|---|---|---|
| `Riptide.fbx` | **the Dolphin's low-poly ancestor** — `Chassis`, `NoseTop/Bottom`, `LeftWing`, `RightWing` and six `Thruster*` where the shipped hull has six `Engine case *`. This is where the class name `RiptideAnimation` comes from. | 1,326 verts against `Dolphin_Test`'s 12,583; no armature, no takes, no shapes. Historically interesting, strictly superseded. |
| `Dolphin_split.fbx` | a 137-vert sea-animal blockout (`Fusilage`, `Dorsalfin`, `LeftTail`, `RightTail`, wings) | no armature, no takes, no shapes, no referrer |
| `Hammerhead_split.fbx` | a 116-vert hammerhead-shark blockout (`Fusilage`, `Head`, wings, `Tail`) | same — and note both of these read as **fauna** blockouts filed under Vessel Models |
| `Vessel_Placeholder_1.fbx` | a third-party download — nodes `supermatic sky cruiser.001/.002`, 110,850 verts, 8.6 MB | its **288 takes are not 288 animations**: 288 stacks × 288 layers × 864 curve-nodes ≈ 3 TRS channels each, the combinatorial one-node-per-take artefact of a Sketchfab/Blender import. No vessel geometry, no armature, nothing the fleet references. Its `.meta` *depends on* `Rhino_Test`/`RhinoModel`, not the reverse. |
| `Vessel_Placeholder_2.fbx` | another third-party download — nodes `Sketchfab_model`, `nitro blade.obj.cleaner.materialmerger.gles`, 68,995 verts | no armature, no takes, no shapes, no referrer. Also remaps materials into `RhinoModel`. |
| `Assets/_Animations/MantaAnimatorController.controller` | a duplicate of the live `_Models/Animations/` controller | referenced by **nothing**; the live one is `_Models/Animations/MantaAnimatorController.controller` (5 vessels). Not a model, but it surfaced from the same sweep and is in the same class. |

⚠ The two placeholders are **third-party Sketchfab assets** sitting in a shipping game's tree.
That is a licensing question as well as a size one (12.2 MB between them), and it is Garrett's call,
not a cleanup decision — flagged, not acted on.

---

## Phase 2 — the Dolphin rig swap  ✅ TOOL SHIPPED · ⚠ NOT YET RUN

The highest-value item, and the only rig carrying a real morph. Phase 0 changed two of this
phase's own premises (`VESSEL_CONSTRUCTION.md` §4.3), both in the easier direction:

- **The rig is the shipped hull, in the same place.** World bounds agree on all six faces to three
  decimals; 8,311 of 12,583 shipped vertices sit within 5.5e-5 of a rig vertex. So **no collider is
  re-fitted** — each gameplay volume is re-homed onto its bone with the world pose preserved. (Do
  not carry that to the Rhino or the Urchin; §4.4.)
- **The rig restores six exhaust nozzles.** 4,284 rig vertices have no counterpart in the shipped
  hull: the six `Engine Left/Right.N` inner meshes, which `Dolphin_Test` ships at `localScale 0.01`
  — 0.00095 units in a 5.3-unit ship, i.e. never drawn. Rendered and confirmed by eye: the shipped
  pods are sealed cones, the rig's have open bells.

**The jets move to different bones than this brief assumed.** The rig's skin weights settle it:
`jetT/jetm/jetB` skin 538 verts each (an engine CASE), `jetint/jetinm/jetinb` skin 712 each (the
restored nozzle). A plume comes out of the nozzle, so `VesselJet.mountBone` is set to the `jetin*`
bone and the jet is parked at the mouth centre measured off that bone's own rearmost lip band.

| bone | mouth centre (vessel space) | current jet sits |
|---|---|---|
| `jetint.l` | (−0.4153, 0.5418, −2.2867) | 0.047 forward |
| `jetinm.l` | (−0.5752, 0.2409, −2.2824) | 0.039 forward |
| `jetinb.l` | (−0.5408, −0.0975, −2.2777) | 0.031 forward |
| `jetint.r` | (0.4152, 0.5420, −2.2867) | 0.055 forward |
| `jetinm.r` | (0.5751, 0.2411, −2.2824) | 0.052 forward |
| `jetinb.r` | (0.5409, −0.0973, −2.2777) | 0.046 forward |

**The tool:** `FrogletTools ▸ Vessels ▸ Swap Vessel Rig` (`VesselRigSwapper`) — dry-run first, then
write; idempotent; refuses and writes nothing if a single mapped bone or object is missing; records
its output to the ledger and draws the ship panel. `VesselRigSwapPlanner` stays as the report-only
sibling and now points at it.

⚠ **The tool has never been run.** This session had no Unity CLI, so the swapper is type-checked
against transcribed stubs and nothing more, and **the branch carries the tool, not a swapped
prefab**. The run-and-verify steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`; use the window's
**Validate & Push** so the prefab lands as its own commit.

Constraints already handled in code, which must not be re-solved:

- `VesselAnimation` resolves parts by name, preferring an authored reference — so the swapper
  **clears** the animation's Transform fields, letting them bind to bones.
- Rest poses are captured (`CaptureRestRotations` / `RotatePartFromRest`), so rigged art holds its
  shape.
- Morph weights are written in `LateUpdate`, keeping the element level authoritative over any stray
  animation curve.

Acceptance: `Audit Vessel Elemental Morphs` reports the Dolphin **with non-zero shape magnitude**
(Phase 4), **and** the hull visibly changes between element level 0 and 10 in play. Both.

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
