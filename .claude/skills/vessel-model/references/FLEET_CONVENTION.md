# The fleet convention: what a vessel model IS

One convention for every Cosmic Shore vessel model, derived from what the game INTENDS (its code, its locked docs, the
studio vision), not from the seven artist files. Numbered; every rule carries a WHY; `file:line` was measured on
`ccr-d93a6dfb-ugbxng`, 2026-10-17; "inference" marks a conclusion. `CONTRACT.md` is the measured state of today's prefabs
(what an in-place write must still keep), `Docs/VesselModels/<Vessel>.md` the dossiers; section 7 maps each difference to
its migration step; where the three readers disagreed, it says so.

## 0. Garrett's rule, and what it changes

> "One thing to be aware of is not overindexing on the flaws in the various vessel implementations. They are
> all a work in progress, so we should be building a framework that meets our intentions not mimic halfbaked
> examples." (Garrett, 2026-10-17)

1. **The bar is this file.** The skill's first draft made the shipped files the law: four export presets because four
   artists exported four ways (`Tools/Build/vessel_model/export.py:67-82`), seven slot tuples (`specs/*.py`), "keep the
   file's case", "reproduce, never normalise" (LESSONS 6, 23). Those are now LEGACY BINDINGS: true of an in-place
   replacement under today's prefab wiring, never of what a model is. A spec declares a binding as a binding
   (`symmetric=False  # LEGACY`, `specs/sparrow.py:307`) with the section 7 step that retires it.
2. **A new hull is built to sections 1-6 with no per-file carve-out.** WHY: the design philosophy forbids context-specific
   exemptions (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md:430-447`), and the studio vision needs a vessel to be DATA a
   generic page generates (`Docs/Studios/VESSEL_STUDIO_VISION.md:54`).
3. **The unification target already written down stands**: one skinned model per vessel, parts are bones resolved by name,
   FX mounts parent to the part, every audit measures the thing (`Docs/VESSEL_CONSTRUCTION.md:1176-1188`).
4. Where CONTRACT.md keeps a quirk (:65-69 "keep the order and the game keeps behaving"), the convention wins (section 7).

## 1. One file, one frame

1. **One file per vessel**, `Assets/_Models/Vessel Models/<Vessel>.fbx`, binary FBX 7400, holding the one skinned mesh, the
   one armature, the four element shapes and every take the vessel plays. WHY: a clip borrowed across files
   (`urchan_shapekey_with_animations.fbx` feeds nine vessels, `SparrowModel4.fbx` the missile bays;
   `Docs/VESSEL_CONSTRUCTION.md:1144-1147`) makes a model with zero prefab references undeletable.
2. **The export preset is `zup` for every new hull** (`export.PRESETS["zup"]`: Z-up, UnitScaleFactor 100 = metres, Front +Y,
   Coord -X, `bake_space_transform`), mesh node AND armature at identity (R 0, S 1), vertices in metres. In Unity: nose +Z,
   up +Y, pilot's left -X (CONTRACT.md:86-89; `ScarabHullBuilder.cs:39` "+Z forward"). In Blender: nose -Y, up +Z, pilot's
   left +X. WHY metres at identity: every vessel `.meta` imports at `globalScale 1`, `useFileScale 1` (CONTRACT.md:46), so
   file units ARE Unity units; a non-unit node scale folds into the bind pose (Manta family x100, bind mesh 0.011 units;
   Sparrow node moved 185 units; `.claude/skills/vessel/SKILL.md:218-226`), gives bones `lossyScale ~100`
   (`Docs/VESSEL_CONSTRUCTION.md:745-750`) and sizes an unauthored jet 40x (`VESSEL_TAIL_AND_JETS.md:144-150`); at identity,
   bone-space offsets, colliders and plume scales mean the same thing on every hull. WHY `zup` over `zup_identity`: the
   Serpent's identity axes put the pilot's RIGHT on Blender +X (CONTRACT.md:77), while Blender's front view and its X-mirror
   / symmetrize tools name the +X side `.L` (Blender convention, not measured in the repo), so under `zup` `.L` means the
   pilot's left with no per-file note; it is the reference hull's (the Squirrel, `VESSEL_TAIL_AND_JETS.md:214-216`) and the
   Sparrow's, reproduced exactly (`rest_compare` 0.000, CONTRACT.md:90-91).
3. **Legacy presets are bindings**: `zup_identity` (Serpent), `yup` (Dolphin, Rhino rigs), `yup_baked` (Manta, Urchin rigs)
   and the armature scars (Sparrow S 0.2034 + 180 deg Z, Squirrel R(71.9,-180,0), CONTRACT.md:93-94); the cm presets' R(-90)
   S(100) roots are the cost item 2 measures. An in-place write keeps them because the prefab's colliders sit in the mesh
   node's local frame and the jets' offsets in bone space (CONTRACT.md:81-84); the section 7 re-fits retire them.
4. **Topology**: manifold, every part grown out of another (nothing floats), no self-intersecting polygons (Unity DROPS
   them: twelve on the Dolphin rig, `Docs/VESSEL_CONSTRUCTION.md:1049-1057`), split normals, one UV set, rigid skin per
   part, <= 4 bones per vertex. WHY: the chase tile is judged on silhouette and connectivity (SKILL.md section 0); a dropped
   face is missing geometry.

## 2. Names and rig roles

1. **Names are the binding; never indices or heuristics.** The prefab reaches into the model only by NAME: parts
   (`VesselAnimation.ResolvePart`, `VesselAnimation.cs:86-105`), jet hosts (`VesselJet.mountBone`,
   `VesselJet.cs:37,172-181`), materials (`.meta externalObjects`; `_domainReplacesMaterials`, `VesselCustomization.cs:33`),
   takes (`.meta clipAnimations`), shapes (`TryResolveElement`). WHY: importer fileIDs come from type + name, not derivable
   offline (CONTRACT.md:22-27); name-heuristic mounts were unshippable (`VESSEL_TAIL_AND_JETS.md:239-254`).
2. **Node names, uniform on every file**: the FBX root is the file; mesh object and geometry `mesh` (the one
   `SkinnedMeshRenderer`; the GameObject `_shipGeometries` names); armature object and data `rig` (so every take is
   `rig|<take>`); root bone `hull`; both are siblings under the root at identity. WHY uniform: one gate checks every file, a
   wirer binds with no per-vessel table, a recipe generates against fixed names (`rig`: reader 2; `mesh`: inference).
3. **Role grammar** `<role>[.<n>][.<T|M|B>][.<L|R>][.mouth|.hold]`: lowercase role, index before side (`wing.2.L`), top /
   middle / bottom before side (`jet.T.L`), `.L/.R` = the PILOT's left / right (Unity -X / +X). `ResolvePart` is
   case-insensitive and first-match (`VesselAnimation.cs:92`), so a driver names roles only; a role the model lacks is
   reported once (`:112`), a role the driver does not know costs nothing.

   | role | the bone | hosts |
   |---|---|---|
   | `hull` | the ONE root bone; skins the fuselage; every other bone descends from it (directly or via `.hold`) | nothing static: colliders, skimmers, the camera anchor and `VesselTail` sit on the vessel root / mesh node, which share `hull`'s identity frame |
   | `nose` | cockpit / head section the pitch puppetry turns | `jaw.T` / `jaw.B` as children where the class bites |
   | `wing.L/.R`, `wing.2.L/.R` | lifting surfaces, fore pair first | a `.hold` parent carries any rest dihedral |
   | `fin`, `fin.L/.R`, `tail`, `tail.2`, ... | stabilisers and the tail chain | |
   | `jet.L/.R`, `jet.T.L`, `jet.B.R`, ... | engine CASE bones the throttle puppetry turns | one `jet.<x>.mouth` child each |
   | `jet.<x>.mouth` | at the nozzle lip, length axis pointing OUT along the exhaust; skins the bell | the `VesselJet` (`mountBone`), pinned at (0,0,0) |
   | `gun.L/.R`, `horn`, `antenna.L/.R`, `leg.<n>.L/.R`, `bay.L/.R`, `shroud.*`, `shield.*` | class parts | ability takes, FX |
   | `<part>.hold` | a parent that skins nothing and carries a rest angle | survives `optimizeBones 1` only as the parent of a skinning child (CONTRACT.md:49-51) |

   WHY a role is what the bone SKINS: the Dolphin's jets belong on the nozzle bones that skin the bells, not the case
   bones (`VesselRigSwapper.cs:33-38`); the Sparrow's `b_Tail*` bones drive its NOSE blades
   (`VESSEL_TAIL_AND_JETS.md:303-309`). WHY `hull` is the single root: the Dolphin driver rebuilds a chassis-child
   hierarchy because its wings and jets are siblings (`RiptideAnimation.cs:130-148`, reader 2); children of `hull` get it free.
4. **Rest pose = flight pose at cruise, ship-aligned and symmetric.** WHY: puppetry is driven RELATIVE to the authored rest
   (`CaptureRestRotations` / `RotatePartFromRest(InFrame)`, `VesselAnimation.cs:148,180,229`); a parent-space Euler on a
   bone resting at (77,0,-180) rolls instead of pitching (`Docs/VESSEL_CONSTRUCTION.md:366-369`); an asymmetric rest (the
   Sparrow's fans, `specs/sparrow.py:307`) stops one pose spec serving both sides.
5. **FX mounts**: the tail on the vessel ROOT at `z = -1.05 x |followOffset.z|` (`VESSEL_TAIL_AND_JETS.md:211-216`); every
   plume a `VesselJet` on a `.mouth` bone at (0,0,0) (`:239-254`), so the FILE answers "where is the engine" (five hulls
   have no jets because their models do not, `:437-449`). A mount measured off static geometry is allowed only where the
   hull genuinely has no bone for the feature (the Rhino's eight body nozzles today,
   `Docs/VESSEL_CONSTRUCTION.md:1184-1187`); a conventional rig gives them `.mouth` bones. Inference: Blender bones import
   with +Y along the bone, so one fleet-wide jet rotation serves; measure it on the first conventional hull.
6. **The model is never only a mesh**: it hosts the hit box, skimmers and the crystal hull-fusion bakes
   (`.claude/skills/vessel/SKILL.md:724-736`); a bake (`CrystalHullFusion.cs:1157-1172`, by shape index) goes stale on every
   mesh write and is re-baked with it.

## 3. Four material slots, one order and meaning

| slot | name | wears | share of faces | runtime writer |
|---|---|---|---|---|
| 0 | `Domain` | the team colour: `ThemeManager` clones `GreenVesselMaterial` and sets `_Color1/_Color2` from the domain (`ThemeManager.cs:47,111-112`); the file's jade is a placeholder | 20-35 %, on the faces the chase camera sees | `ApplyShipMaterial`; `FlareBody` (`materials[0]`, `VesselAnimation.cs:537-548`) |
| 1 | `Body` | `BlueBaseVesselMaterial`, never repainted | the majority | none |
| 2 | `Window` | `ScreenVesselMaterial`: canopy, eyes, screens | >= 2 % (4-10 polygons is no cockpit) | none |
| 3 | `Engine` | `EngineVesselMaterial` / `LimeVesselMaterial`: mouths, bells, boost stripes at the stern | 3-10 % | `FlareEngine` (`materials[3]`, `:527-535`) while `throttleMultiplier > 1` (`VesselTransformer.cs:1466-1470`) |

1. **Exactly these four names, PascalCase, no `.NNN` suffix, every slot owning polygons** (Unity drops an empty slot and
   shifts the array, CONTRACT.md:69-70). WHY this order: it is what `FlareBody[0]`, `FlareEngine[3]` and the
   SkinnedMeshRenderer branch of `ApplyShipMaterial` (`VesselHelper.cs:162-167`) already assume, what the Squirrel ships (26
   / 64 / 3 / 7 %, CONTRACT.md:57) and what `materials.py:4-14` documents as the game meaning.
2. **Paint by IDENTITY, not index**: every prefab authors `_domainReplacesMaterials = [GreenAccentVesselMaterial]` and
   `_shipGeometries = [mesh]`, the GameObject carrying the renderer, never the FBX root (the Sparrow names the root and is
   never painted, `Docs/VesselModels/Sparrow.md:205`). WHY: the slot-1 default "is not a contract, it is a coincidence"
   (`Docs/VESSEL_CONSTRUCTION.md:345-349`); with the order above the index and identity paths agree.
3. **The `.meta` remaps ALL FOUR names** (`externalObjects`). WHY: an unremapped slot imports the DCC material, which lacks
   `_ColorMultiplier`: the boost flare no-ops (Dolphin `N.001`, `Docs/VesselModels/Dolphin.md:81,169-170`).
4. **No `m_Materials` override on the hull renderer in the prefab**: file + meta are the single source of what the hull
   wears (the Squirrel's three overrides and the Manta's rotated inline array are section 7 rows).
5. Code follow-up (proposal; owner to confirm: Garrett, last author of `VesselAnimation.cs`): flares and the painter resolve
   slots by material identity for every renderer kind and write `_ColorMultiplier` through `sharedMaterials` +
   `MaterialPropertyBlock` (`ScarabAnimation.cs:552-556` is the reference).

## 4. Four element shape keys

1. **Names** `Charge`, `Mass`, `Space`, `Time`, in that order (= `MorphElements`, `VesselElementalMorphConfigSO.cs:40-41`,
   so shape index == element index), on the one skinned mesh, no deformer prefix, no element token on any other shape
   (`TryResolveElement`, `:60-98`: the tail after the last `.` or exactly one whole token, case-insensitive; two tokens =
   ignored). Readers disagreed on case (one proposed lowercase); the resolver is case-insensitive, so the enum spelling is
   the pick.
2. **One frame per shape, last-frame weight 100, per-vertex normals, nothing below the basis**: the basis IS level 0 and the
   renderer weight equals level x 10 (`VesselAnimation.cs:511-513`; `VesselElementalMorphConfigSO.cs:35-37,47-48`). Zero
   blend-shape curves in any take (`LateUpdate` is the single writer, `VesselAnimation.cs:484-491`).
3. **Magnitude**: farthest-vertex travel 4-12 % of the mesh AABB diagonal measured OFFLINE (`fbx_probe`), floor 3 %. WHY:
   the in-editor audit divides by root-bone-space bounds, 1.283x smaller on the Dolphin
   (`Docs/VESSEL_CONSTRUCTION.md:298-301`), and its INERT line is 0.1 % (`VesselElementalMorphAuditor.cs:55`, not the "~2 %"
   SKILL.md:87 claims; a doc fix); a shape that moves the whole mesh reads as a scale, not an element (LESSONS 20). The
   decisive gate is the chase tile: a pilot names the element from the silhouette.
4. **What each expresses**, the fleet rule (`Docs/ElementalAbilitySystem/ARCHITECTURE.md:52-53`): **Space** = reach:
   extremities extend along their own axes, the footprint grows, nothing thickens. **Time** = rate: sharpen and sweep (nose
   tapers forward, wings rake aft, bells lengthen), never bigger. **Charge** = threat: the WEAPON surfaces grow (gun, spike,
   jaw, horn, pod mouth). **Mass** = volume: the body fattens about its centre, wings and weapons untouched. The part each
   one grows is the part that element's ability lives on (`python3 Tools/Build/element_ability_table.py <Vessel>`: Squirrel
   Charge = Crystal Joust -> ram and pods, Dolphin Charge = Echo Sight -> jaws), so the morph names what grew.
5. A hull that stays procedural morphs through `IProceduralElementMorphSource` (`IProceduralElementMorphSource.cs:17-26`)
   and says so. Morph writes mesh / localPosition, puppetry writes localRotation; no channel has two writers.

## 5. The nine input takes and the ability takes

1. **Names**, exactly (`takes.py:30-31`): `idle`, `pitchup`, `pitchdown`, `yawleft`, `yawright`, `rollleft`, `rollright`,
   `speedup`, `speeddown`, exported as `rig|<name>`: single-frame POSES at frame 0, every deforming bone keyed, no
   blend-shape curves; the `.meta` pins `rig|<name> -> clip id`.
2. **Meaning in the Unity chase view**: `pitchup` = the full-stick pose while the nose RISES (tail drops); `yawright` = the
   nose swings to the pilot's right (+X); `rollright` = the `.R` wing DROPS; `speedup` = full-throttle streamline (sweep
   back, tuck); `speeddown` = brake (fan open, pods out); `idle` = rest = cruise; mirrors for the rest. A diagonal is the
   blend, never a take (the Sparrow's `Speed+*` set is legacy).
3. **Authoring in the `zup` Blender frame** (nose -Y, up +Z, `.L` +X): pitch about world X, roll about Y, yaw about Z
   (`takes.world_to_bone`; CONTRACT.md:109-110). Inference from the frame: nose-up = negative about X (CONTRACT.md:110
   agrees), nose-right = negative about Z, roll-right = negative about Y; confirm yaw and roll on the first pose row.
4. **Amplitude is fleet-scale**: the largest part deflects >= 20-30 deg, calibrated on `RhinoAnimation` (`yawAnimationScaler
   = 80`, `RhinoAnimation.cs:14`; 14-26 deg reads as dead, `.claude/skills/vessel/SKILL.md:339-351`). Never bake a driver's
   bug into a pose (the Squirrel's top tree halves every pose; SKILL.md section 5).
5. **Ability takes**, same file: `boost` (loops), `bay.L` / `bay.R` (one-shot; replaces `SparrowModel4`'s "Missile Launch 2
   / 1", `SparrowAnimationController.cs:76-77`), `charge` / `fire`, `brake`; additive layer, no shape curves.
6. **Sign convention at the driver** (reader 2's proposal, inference; owner Garrett): the puppetry receives two pitch signs
   today (two-stick `YSum` is negated, `DualStickMix.cs:46`, `VesselTransformer.cs:921-926`; single-stick `EasedLeft.y` is
   not, `SingleStickVesselTransformer.cs:26-28`), the Serpent / Sparrow / Manta blend trees absorb it by placing `pitchup`
   at (0,-1) where the Squirrel's sits at (0,+1), `MantaAnimationContoller.cs:26-29` doubles and negates while its `Idle`
   does not (`:42`), and `Idle` is touch-only (`TouchInputStrategy.cs:218`). The convention normalises ONCE in
   `VesselAnimation.Update` (pitch +1 = nose up, yaw +1 = nose right, roll +1 = bank right, throttle 0 = cruise) into one
   shared Animator graph (the Squirrel's placement) plus a per-vessel override controller, with no `Idle` branch; the
   model's side is done when the nine takes carry item 2's meanings.

## 6. Sizes and cameras per vessel class

Every proportional number derives from the class camera `|followOffset.z|`: tail z = -1.05x, ribbon `widthScale` = /20,
plume scale = (0.6, 0.6, 0.13) x /20 (`VESSEL_TAIL_AND_JETS.md:117-142,211-216`). WHY: a distance authored in world units is
a silent dependency on every scale between asset and scene (`Docs/VESSEL_CONSTRUCTION.md:717-721`).

| class (sticks) | camera z | hull length nose->tail (m) | tail z | width x | source |
|---|---|---|---|---|---|
| Urchin (2) | -6.67 | ~1.8 (ball R 0.425, spines +-0.9) | -7.0 | 0.33 | CONTRACT.md:132; TAIL:226 |
| Squirrel (2) | -17 | 6.5 | authored pair (+-4, 0, -12), kept | 0.85 | CONTRACT.md:128; TAIL:214-216 |
| Dolphin (2) | -20 | 5.3 | -21 | 1.0 | CONTRACT.md:129; TAIL:223 |
| Manta, Falcon, Shrike, Termite (2 / 1) | -30 | open (two coincident hulls) | -31.5 | 1.5 | TAIL:230,233 |
| Grizzly (1) | -30 | placeholder | -31.5 | 1.5 | TAIL:228 |
| Sparrow (1), Scarab (1) | -50 | 11.5 | -52.5 | 2.5 | CONTRACT.md:131; TAIL:224,229 |
| Rhino (2) | -70 (the asset; TAIL:227 and Dolphin.md:207 say -120, stale) | 16.8 fuselage, 20.4 composite | -126, derived for -120: re-derive | 6.0 | `Docs/VesselModels/Rhino.md:192` |
| Serpent (1) | -250 | 11.6 | -262.5 | 12.5 | CONTRACT.md:130; TAIL:231 |

Inference from the rows whose camera was tuned to the hull: length / |camera| sits at 0.23-0.38 (Squirrel 0.38, Dolphin
0.27, Urchin 0.27, Rhino 0.24-0.29, Sparrow 0.23); a new hull is authored in that band. The Serpent's 0.05 is the outlier
its own docs call a disagreement (`VESSEL_TAIL_AND_JETS.md:437-449`). One-thumb classes (`IsSingleStickControls`: Sparrow,
Serpent, Grizzly, Termite, Falcon, Shrike, Scarab; `Docs/claude/ABOUT_PROJECT.md:139`) receive roll 0 and throttle 0 today
(`VesselAnimation.cs:33`), so their roll / speed takes are authored but idle until section 5 item 6 lands.

## 7. Legacy bindings per shipped vessel

Each row: what still differs from sections 1-6, and the step that retires it. An in-place overwrite today keeps every
binding (same guid, names, rest poses, frame); nothing in this table is a rule for a new hull.

| vessel (dossier) | differs from the convention (why the prefab binds it) | migration step |
|---|---|---|
| Squirrel (`Docs/VesselModels/Squirrel.md`) | names `a_SquirrelShipMesh_nearfinal`, `a_Squirrel_Armature`, `bbone_*` / `abone_MAIN_Cockpit` (:117,:242; fileID hashes); armature R(71.9,-180,0); takes `a_Squirrel_Armature\|squirrelanim_*`; meta remaps three of four slots and the prefab overrides slots [0],[1],[3] (`Squirrel.prefab:3912-3926`); `_domainReplacesMaterials` empty; driver x2 / negate against a halving top tree, state speed 50 | slots and shapes already canonical. Prefab re-fit: add the `Engine` meta row, author the identity list, delete the [0] / [3] overrides (ask Garrett about the opaque-body [1]); shared graph + override controller; rename nodes and takes only on a re-wire |
| Dolphin (`Dolphin.md`) | `yup` frame, roots R(-90) S(100); slots `accent.001 / BASE.001 / windsheild.001 / N.001`, `N.001` unremapped so `FlareEngine` is inert (:81,:169-170); lowercase shapes; window 4 polys; takes `dolphin\|<pose>_dolphin` (10 motions); bones `jetT/jetm/jetB`, nozzles `jetint/jetinm/jetinb`, `jaw.u/jaw.b`; the driver rebuilds a chassis hierarchy (`RiptideAnimation.cs:130-148`) | identity list already authored. Re-wire: rename slots + four meta rows, re-bake the four `Dolphin_*_HullFusionBake` assets, `.mouth` names on the six nozzle bones (`VesselJet.mountBone` in `Dolphin.prefab`), `hull` parenting, `zup` with colliders re-fitted |
| Serpent (`Serpent.md`) | `zup_identity` (head on Blender +Y); slots `Body, Engine, Window, Domain` and no identity list, so the index path paints the artist's Body, 64 % of the area (:208); `Engine` unremapped (`Engine_2.mat` orphan); takes `SerpentArmature\|Serpent{Up,Down,Left,Right,Idle,SpeedIncrease}Anim`, pitch placed inverted, no roll / throttle tree; the static `serpent.fbx` half (16 meshes, FBX 7700) still in the prefab (`Docs/VESSEL_CONSTRUCTION.md:61-68`); a 250 camera on an 11.6 hull | now: author `_domainReplacesMaterials` on `Serpent.prefab`. Re-wire: reorder slots to canonical (the meta binds by name), add the `Engine` row, retire `serpent.fbx` into the skinned model, `zup`, rename takes, re-fit the camera |
| Sparrow (`Sparrow.md`) | armature S 0.2034 + 180 deg Z, mesh node moved 185 units; `b_Tail*` bones drive the NOSE blades and host the jets (`VESSEL_TAIL_AND_JETS.md:303-309`); slots `Body, Domain, Engine, Window`; `_shipGeometries` = the FBX root so the domain is NEVER painted (:205) and `FlareEngine` flashes the glass (:170); asymmetric fan rest (`specs/sparrow.py:307`); takes `Sparrow_Armature+Mesh\|*_SparrowAnim` incl. `Speed+*`; missile clips from `SparrowModel4`; Roll / Throttle tree with four empty motions | now: `_shipGeometries -> mesh node` + identity list. Re-wire: canonical slots + `Engine` row, `jet.<n>.<side>.mouth` names, re-rest the fans symmetric, `bay.L/.R` takes in the same file, drop `SparrowModel4` |
| Manta family (`Manta.md`; Falcon, Shrike, Termite are flat copies) | two coincident hulls (`mantis_shapekey_with_animations` + `Manta_shapekey_rigged`, OPEN, `Docs/VESSEL_CONSTRUCTION.md:135-153`); `yup_baked`, armature x100, wing colliders 0.004 u; slots `accent / BASE / windsheild / N` with the prefab's unpacked array rotated one slot so the body wears the Window material (:83,:261); mixed-case shapes; `MantaAnimation` swaps yaw and roll through the 4-arg `RotatePart` (:120), authored GameObject refs, no `ResolveParts`; the live Animator has no controller; no jets | decide which hull stays, then: identity list + fix the inline array, delete the inactive duplicate and its dead controller, a role-resolving driver, `.mouth` bones for jets, `zup` at unit scale (Affects: Garrett's fusion bakes, Shombith's #1067 area, the three copies) |
| Urchin (`Urchin.md`) | prefab draws `Urchan_Test.fbx` part-per-mesh with 13 authored part fields (:115), no SMR (no flares), engine nodes at 1.75 scale (jets author (0.1145, 0.1145, 0.0248)); an orphan Animator with `MantaAnimatorController` (:135); the rebuilt `urchan_shapekey_with_animations.fbx` is `yup_baked`, slots `accent / BASE / windsheild / N`, empty meta, bones `gunM.*`, `jetT/jetB.*`, `wingconrot*`, `sheildconrot*`; working-tree takes `Armature\|<pose>` vs HEAD's `urchan_*` with shape curves (reconcile) | editor rig swap at the decided `localScale 0.474905` (`Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md:227-247`): rename / remap the four materials, set `VesselAnimation.SkinnedMeshRenderer`, delete the orphan Animator, roles `gun.L/.R`, `jet.T/.B.<side>` + `.mouth`, unit-scale engine nodes; the morph audit then MEASURES travel (FOLLOWUP.md:234-236 "keep reporting un-morphed" is stale once real morphs swap in) |
| Rhino (`Rhino.md`) | prefab draws `Rhino_Test.fbx`; the index path paints `BASE` = 80 % of the hull (:102-104), pods not in `_shipGeometries`, no SMR; eight body-nozzle jets measured off `fusalage`, wing-pod jets on legacy GameObjects; the rebuilt rig is `yup`, slots `accent.005 / ... / N.005`, empty meta, bones `wing1.*`, `jet.*`; `RhinoAnimation` holds the wings `(-1 + throttle) * 80` off rest at a centred stick (:41); camera -70 in the asset, -120 in docs | editor rig swap at the decided `z -1.5545` (FOLLOWUP.md:227-247): canonical slots + meta rows + identity list, `wing.L/.R`, `wing.2.L/.R`, `jet.L/.R` + `.mouth`, `.mouth` bones for the eight nozzles so the static fallback retires, normalised throttle; read `cece/relaxed-hopper-r62hbd` (11 unmerged Rhino commits) first |
| Grizzly, Scarab, Stoat, Thresher, Butterfly, BrittleStar | `Vessel_Wedge_Scene (4)` part-per-mesh x8 (Grizzly, blocked on art); C# runtime hulls judged "terrible even as placeholders" (SKILL.md section 0), MeshRenderer two-submesh hulls painting slot 1 (`ScarabHullBuilder.cs:30-34`), the Scarab's renderers-off Sparrow wrapper; `BufoAnimation` absolute `RotatePart` with roll = -yaw | real FBX under this convention; until then author `_domainReplacesMaterials` on each so identity is the only rule; keep `IProceduralElementMorphSource` only for a hull that stays procedural |

Fleet-wide code bindings, not per vessel: `ApplyShipMaterial` SMR `materials[0]` vs MeshRenderer slot 1
(`VesselHelper.cs:156-178`); `FlareEngine` / `FlareBody` fixed indices through `.materials` (`VesselAnimation.cs:527-548`);
the `VesselCustomization.cs:22-32` tooltip names three roles and no Engine; `materials.py:50-57 slot_names_for` contradicts
`specs/serpent.py:44` and `specs/sparrow.py:44` (delete it: `GENERIC` is the one order, `ALIASES` :41-45 move to the legacy
layer); two drivers nothing references (`DolphinAnimation.cs`, `SingleStickAnimationController.cs`); 10-15 orphan trees per
controller; `VESSEL_CONSTRUCTION.md:342` lists the Manta prefab array as the FBX order.

## 8. What the pipeline already enforces vs what still needs a gate

Enforced by `export.verify` (`export.py:245-330`; PIPELINE.md "The verify gate"): the spec's bones / armature / mesh node
present; one skinned mesh; every slot owns polygons and its material exists; every spec'd shape present and travelling >=
`min_travel` (default 2 %, specs 2.5-4 %); every spec'd take present, a pose deflects something, zero blend-shape curves;
GlobalSettings and the mesh node's (and a cm rig's armature's) TRS match the preset. `rest_compare.py` proves a reused rig,
`build.py --check` is the drift gate, and `measure_vessel_models.py` plus the editor audits measure magnitude, not labels
(`Docs/VESSEL_CONSTRUCTION.md:185-190`).

Needs a gate (none of these fails today):
1. Preset `zup` for a NEW hull with BOTH roots at identity (today the armature TRS is asserted only on the cm rigs); a
   legacy preset accepted only when the spec marks it `LEGACY` with its section 7 step.
2. Node names `mesh`, `rig`, `hull`; bones match section 2's grammar; `hull` the single root; each jet bone a `.mouth` child.
3. Slot names exactly `Domain, Body, Window, Engine` in that order (today any spec'd names pass); section 3 shares.
4. Shapes exactly `Charge, Mass, Space, Time` in that order, last-frame weight 100, normals present, travel 3-12 % offline,
   no element token on any other shape (today: presence and `min_travel` only; the editor's floor is 0.1 %).
5. Takes exactly `rig|<pose>`, every bone keyed, largest deflection >= 20 deg; ability takes per section 5.
6. Hull length / |class camera| inside 0.23-0.38 (section 6).
7. Prefab side, in the editor (`FrogletTools > Vessels > Audits`): `_domainReplacesMaterials` authored, `_shipGeometries`
   names the renderer's GameObject, all four `.meta` remaps present, no `m_Materials` override on the hull renderer, every
   `VesselJet.mountBone` resolves to a `.mouth` bone at (0,0,0), fusion bakes fresh.
8. Driver side: one pitch sign at the dispatch and no touch-only `Idle` path (section 5 item 6).

Every new gate is watched failing on a negative control before its green counts (`Docs/VESSEL_CONSTRUCTION.md:1188`).
