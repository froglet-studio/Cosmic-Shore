# The fleet convention: what a vessel model IS

One convention for every Cosmic Shore vessel model, derived from what the game INTENDS (its code, its locked docs, the studio vision), not from the seven artist files. Numbered;
every rule carries a WHY; `file:line` was measured on `ccr-d93a6dfb-ugbxng`, 2026-10-17; "inference" and "proposal" mark what is not measured. Pipeline files are cited by SYMBOL
(`export.verify`, `takes.STANDARD_POSES`): they are being edited concurrently (`git status`: `build.py`, `export.py`, `materials.py`, `takes.py`, `rig.py` all `M`). `CONTRACT.md`
is the measured state of today's prefabs (what an in-place write must still keep), `Docs/VesselModels/<Vessel>.md` the dossiers; section 9 maps each difference to its step.

## 0. Garrett's rule, and what it changes

> "One thing to be aware of is not overindexing on the flaws in the various vessel implementations. They are all a work in progress, so we
> should be building a framework that meets our intentions not mimic halfbaked examples." (Garrett, 2026-10-17)

1. **The bar is this file.** The skill's first draft made the shipped files the law; those are now LEGACY BINDINGS, true of an in-place replacement under today's prefab wiring,
   never of what a model is. **The LEGACY marker** is the spec key `export.verify` already reads as an escape hatch, with the section 9 step in its comment: `slot_reorder_from`
   (`specs/urchin.py:195`, the one worked example today), `size_ref_blender` / `size_tol`, `material_colours_ref`; a legacy preset gets `preset_legacy` the same way (gate 1,
   proposal). No spec declares `symmetric` (grep: 0 hits); the first draft's `symmetric=False # LEGACY` example was wrong.
2. **A new hull is built to sections 1-8 with no per-file carve-out.** WHY: the design philosophy forbids context-specific exemptions
   (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md:430-447`); the studio vision needs a vessel to be DATA a generic page generates with no code
   (`Docs/Studios/VESSEL_STUDIO_VISION.md:54`, B3), so a recipe must be able to name every part of a model.
3. **The unification target already written down stands** (`Docs/VESSEL_CONSTRUCTION.md:1176-1187`): one skinned model, bones by name, FX on the part, audits measure the thing.
4. **Three surfaces read the model** (`/vessel-studio` D37, `SKILL.md:109`): Unity's importer; Amoebius's own reader (`Port/src/CosmicShore.Content/Models/FbxReader.cs:11-14`;
   `FbxModelImporter.cs:298-309,638,719` skin + blend shapes; `FbxAnimationImporter.cs:7-19` takes per `clipAnimations`); and the web studio, which loads NO model today (no
   GLTFLoader / FBXLoader under `Docs/Studios/VesselStudio/`; tier A is "Blocks (three.js)", `VESSEL_TEST_RANGE_PLAN.md:22`; a card with no preview code gets "the generic hull in
   its accent", vessel-studio `SKILL.md:502`). Section 7 gives it one.
5. **OPEN for Garrett: one file per vessel, or a shared hull family plus a recipe (a variant by parameters).** Falcon, Shrike and Termite are flat copies of the Manta, the Stoat
   and Scarab wrap donors, the Goby nests the Squirrel's FBX (section 9), and B2 / B9 want hundreds of vessels from presets and dice; rule 1.1 forbids the cheap case. The
   alternative: the generator writes one file per vessel FROM its spec, the spec being the recipe's hull section (section 8). Until decided, rule 1.1 stands.

## 0.5 The hovering assembly (Garrett, 2026-10-21)

A vessel is a core plus distinct parts that float clear of it, each on its own bone and animated together (SKILL 0.5,
measured band there). Rule for a new hull: every wing, fin, engine pod, gun and tail segment is its own closed shell
0.5-6.5 % of the diagonal clear of its neighbours; plates within a part may sit on 0.1-0.2 % seams; nothing is welded,
strutted or filleted on. The parts' bones are named for the code that moves them (section 2), and the gate is
`assembly_report.py --floating` (section 10, gate 12).

## 1. One file, one frame

1. **One file per vessel**, `Assets/_Models/Vessel Models/<Vessel>.fbx`, binary FBX **7400**, holding the one skinned mesh, the one armature, the four element shapes and every
   take the vessel plays. WHY 7400: the repo's writer refuses 7500+ (`Tools/Build/fbx_binary.py:115-117`) and both readers take it (Unity; Amoebius reads 7100-7400 and 7500+).
   WHY one file: a clip borrowed across files makes a model with zero prefab references undeletable, and today TWO foreign files are: `urchan_shapekey_with_animations.fbx`
   (`Docs/VESSEL_CONSTRUCTION.md:1146`) and `mantis_shapekey_with_animations.fbx` (guid 7c324ef5, in the Roll/Throttle and Yaw/Pitch trees of all four controllers, e.g.
   `SquirrelAnimatorController 1.controller:27-57`), plus `SparrowModel4.fbx` (:1147) and `SparrowModel1` clips in `SerpentAnimController`.
2. **Preset `zup`** (`export.PRESETS["zup"]`: Z-up, UnitScaleFactor 100 = metres, Front +Y, Coord -X, `bake_space_transform`): mesh node at identity, vertices in metres; **the
   armature node carries R(0,0,180) S 1**, the exporter's space transform, which Blender cannot bake into an armature. MEASURED: a fresh `_wedge` build under `zup` (identity
   armature in Blender) writes `WedgeArmature [Null] R=(0,-0,180)`, `WedgeMesh` identity (`fbx_probe`, this session), so the Sparrow's "180-degree Z turn" (CONTRACT.md:93) is the
   preset, not an artist scar; bone WORLD frames stay vessel-aligned. In Unity: nose +Z, up +Y, pilot's left -X (CONTRACT.md:86-89); in Blender: nose -Y, up +Z, pilot's left +X.
   WHY metres at identity: every `.meta` imports at `globalScale 1`, `useFileScale 1` (CONTRACT.md:46); a non-unit node scale gives bones `lossyScale ~100`
   (`Docs/VESSEL_CONSTRUCTION.md:745-750`) and sizes an unauthored jet 40x (`VESSEL_TAIL_AND_JETS.md:144-150`). WHY `zup` over `zup_identity`: Blender's front view and X-mirror /
   symmetrize tools name the +X side `.L`, so `.L` is the pilot's left with no per-file note; `zup_identity` (the Serpent, CONTRACT.md:77) puts the pilot's RIGHT on +X. Evidence:
   the Squirrel and Sparrow reproduce under it (`rest_compare` 0.000, CONTRACT.md:90-91). Gate 1 asserts the armature row.
3. **Legacy presets are bindings**: `zup_identity` (Serpent), `yup` (Dolphin, Rhino rigs), `yup_baked` (Manta, Urchin rigs), the Sparrow's armature S 0.2034 and the Squirrel's
   R(71.9,-180,0) (CONTRACT.md:93-94). An in-place write keeps them because the prefab's colliders sit in the mesh node's local frame and the jets' offsets in bone space
   (CONTRACT.md:81-84).
4. **Topology and budget**: manifold, every part grown out of another, no self-intersecting or n-gon faces (Unity fan-triangulates and DROPS self-intersecting ones,
   `Docs/VESSEL_CONSTRUCTION.md:1049-1057`), split normals, one UV set, rigid skin per part, <= 4 bones per vertex, ONE `SkinnedMeshRenderer`, exactly four shapes. **Ceiling
   (inference, from the Squirrel: 13,660 tris, `Docs/VesselModels/Squirrel.md:86`; 42 bones): <= 14k triangles, <= 42 bones.** WHY a ceiling: no Spacevessels prefab has a
   `LODGroup` (grep: none), no perf doc names a budget, shipped hulls span 6.7k (`Manta.md:75`) to 26k tris (`Sparrow.md:93`), and the Mobile-low tier
   (`VESSEL_TEST_RANGE_PLAN.md:73`) and B9's automatic checks (`VISION.md:60`) need a number a dice roll cannot exceed.

## 2. Names and rig roles

1. **Names are the binding; never indices or heuristics.** The prefab reaches into the model only by NAME: parts (`VesselAnimation.ResolvePart`, `VesselAnimation.cs:86-105`), jet
   hosts (`VesselJet.mountBone`, `VesselJet.cs:37,172-181`), materials (`.meta externalObjects`; `_domainReplacesMaterials`, `VesselCustomization.cs:33`), takes (`.meta
   clipAnimations`), shapes (`TryResolveElement`). WHY: importer fileIDs come from type + name (CONTRACT.md:22-27); name-heuristic mounts were unshippable
   (`VESSEL_TAIL_AND_JETS.md:239-254`).
2. **Node names, uniform on every file**: mesh object and geometry `mesh` (the one SMR; the GameObject `_shipGeometries` names); armature object and data `rig` (every take is
   `rig|<take>`); root bone `hull`; both siblings under the file root. WHY: one gate checks every file, a wirer binds with no per-vessel table, a recipe generates against fixed
   names. The pipeline's own template contradicts this today (`specs/_wedge.py:66-72`: `WedgeArmature`, `WedgeMesh`, `fuse`, `wing.l`; `rig.py:4-8` calls the legacy names "the
   contract"): gate 2.
3. **Role grammar** `<role>[.<n>][.<T|M|B>][.<L|R>]`, a `.mouth` child named `<bone>.mouth`: lowercase role, index before side (`wing.2.L`), top / middle / bottom before side
   (`jet.T.L`), `.L/.R` = the PILOT's left / right (Unity -X / +X). Pipeline change (gate 2): `takes.mirror_pose` swaps by `endswith` with a lowercase default, so `jet.T.L.mouth`
   never mirrors and `wing.L` only does when a spec passes `lr`; it becomes a token swap on `.L`/`.R` anywhere in the name, default upper.

   | role | the bone | hosts |
   |---|---|---|
   | `hull` | the ONE root bone; skins the fuselage; every other bone descends from it | nothing static (rule 2.6) |
   | `nose` | cockpit / head section the pitch puppetry turns | `jaw.T` / `jaw.B` children where the class bites |
   | `wing.L/.R`, `wing.2.L/.R` | lifting surfaces, fore pair first | |
   | `fin`, `fin.L/.R`, `tail`, `tail.2`, ... | stabilisers and the tail chain | |
   | `jet.L/.R`, `jet.T.L`, `jet.B.R`, ... | engine CASE bones the throttle puppetry turns | one `.mouth` child each |
   | `jet.<x>.mouth` | at the nozzle lip, length axis OUT along the exhaust; skins the bell | the `VesselJet` (`mountBone`), pinned at (0,0,0) |
   | `gun.L/.R`, `horn`, `antenna.L/.R`, `leg.<n>.L/.R`, `bay.L/.R`, `shroud.*`, `shield.*` | class parts | ability takes, FX |

   A modeller MAY add a `<part>.hold` parent that skins nothing and carries a rest angle (it survives `optimizeBones 1` only as the parent of a skinning child,
   CONTRACT.md:49-51); it is not part of the grammar: the driver already conjugates rest angles (`RotatePartFromRestInFrame`, `VesselAnimation.cs:229`). `ResolvePart` is
   case-insensitive, first-match, candidates in priority order (`:81-83,92`) but not grammar-aware (`wing.L` != `wing1.l`): **every driver lists the convention name FIRST and its
   legacy names after** (today each names the legacy bone first: `RhinoAnimation.cs:23-26`, `RiptideAnimation.cs:306-321`, `UrchinAnimation.cs:33-49`,
   `ScarabAnimation.cs:184-193`; section 9 rows carry that edit beside the model rename). WHY a role is what the bone SKINS: the Dolphin's jets belong on the nozzle bones that
   skin the bells (`VesselRigSwapper.cs:33-38`); the Sparrow's `b_Tail*` bones drive its NOSE blades (`VESSEL_TAIL_AND_JETS.md:303-309`). WHY `hull` is the single root: the
   Dolphin driver reproduces the chassis-child motion by composition because its wings and jets are siblings (`RiptideAnimation.cs:128-142`); children of `hull` get it free.
4. **Rest pose = flight pose at cruise, ship-aligned and symmetric.** WHY: puppetry is driven RELATIVE to the authored rest (`CaptureRestRotations` /
   `RotatePartFromRest(InFrame)`, `VesselAnimation.cs:148,180,229`); a parent-space Euler on a bone resting at (77,0,-180) rolls instead of pitching
   (`Docs/VESSEL_CONSTRUCTION.md:366-369`); an asymmetric rest stops one pose spec serving both sides.
5. **FX mounts**: the tail on the vessel ROOT at `z = -1.05 x |followOffset.z|` (`VESSEL_TAIL_AND_JETS.md:211-216`); every plume a `VesselJet` on a `.mouth` bone at (0,0,0)
   (`:218-220`), so the FILE answers "where is the engine" (five hulls have no jets because their models do not, `:437-449`). A mount measured off static geometry is allowed only
   where the hull genuinely has no bone for the feature (the Rhino's eight body nozzles, `Docs/VESSEL_CONSTRUCTION.md:1183-1186`).
6. **Who sizes the hit box, skimmer and camera anchor.** They sit on the vessel root / mesh node (which share `hull`'s identity frame), never on a bone, and for a vessel made
   from data **the wirer derives them**: the `BoxCollider` from the `hull` bone's skinned bounds, the skimmer / occlusion-corridor radius from the hull's bounding sphere
   (`PrismOcclusionConfigSO.cs:79-92` falls back to 6 u, clamped 0.25-80, when it cannot measure), the camera from section 6. The crystal hull-fusion bake
   (`CrystalHullFusion.cs:1157-1172`, by SHAPE INDEX) exists for Dolphin, Falcon, Grizzly, Manta, Rhino, Scarab, Serpent, Shrike, Sparrow, Squirrel, Termite and Urchin
   (`Assets/_SO_Assets/CrystalHullFusion/*_HullFusionBake.asset`), goes stale on every mesh write and is re-baked with it.

## 3. Four material slots, one order and meaning

| slot | name | wears (the ONE `.mat`; the file carries no material of its own) | projected AREA on the chase tile | runtime writer today |
|---|---|---|---|---|
| 0 | `Domain` | `GreenAccentVesselMaterial`, the placeholder `ThemeManager` repaints from the domain (`ThemeManager.cs:47,111-112`; tooltip `VesselCustomization.cs:22-32`) | the band a pilot reads at 250 px: a floor only, judged on the tile (proposal: >= 10 % of area, `Manta.md:261`'s number) | `ApplyShipMaterial`; `FlareBody` (`materials[0]`, `VesselAnimation.cs:537-548`) |
| 1 | `Body` | `BlueBaseVesselMaterial` (or its opaque twin `BlueBaseOpaqueVesselMaterial`), never repainted: neutral Blue, dark gunmetal with a steel rim (`vessel_colors.py`, `Docs/VesselModels/COLOR.md`) | the rest | none |
| 2 | `Window` | `ScreenVesselMaterial`: canopy, eyes, screens; neutral Blue, saturated sapphire, told from the body by saturation and value | floor: `export.verify` `min_slot_share` (0.3 % of area) | none |
| 3 | `Engine` | `EngineVesselMaterial`: a low-value Fire ember at rest, multiplier 1 (Garrett 2026-10-18: the engine material "should have been the danger hue (more red less orange), but also less saturated so it reads as thematically dangerous if you stand there but not trying to get your attention"), hot under `FlareEngine`'s x5. **Never `LimeVesselMaterial`**: Lime is the call-to-action colour only (the Manta family's `N -> Lime` and the Rhino's `N.005 -> Screen` were moved to `EngineVesselMaterial` 2026-10-18) | floor as above | `FlareEngine` (`materials[3]`, `:527-535`) while `throttleMultiplier > 1` (`VesselTransformer.cs:1474-1478`) |

1. **Exactly these four names, PascalCase, no `.NNN` suffix, every slot owning polygons** (Unity drops an empty slot and shifts the array, CONTRACT.md:69-70). **WHY one order**:
   so every gate, wirer and reader (Unity, Amoebius's `ModelMaterialUsage`, the studio) reads a slot the same way; NOTHING binds the index once rule 3.5 lands. The index readers
   today: `VesselAnimation.cs:527-548`, `VesselHelper.cs:162-167` and their Port twins `Port/src/CosmicShore.Game/Controller/Animation/VesselAnimation.cs:129-148`,
   `.../Vessel/VesselHelper.cs:123`.
2. **Share is AREA, not polygons** (SKILL.md section 0: the domain band reads at the chase camera): the Rhino's slot 0 is 11.1 % of area while its paint lands on 80 %
   (`Docs/VesselModels/Rhino.md:102`); the Serpent's Body is 47 % of polys and 64 % of area (CONTRACT.md:58 vs `Serpent.md:208`). `export.verify` already measures
   `material_area_share` on the Blender re-import and enforces the floor; the first draft's 20-35 / 3-10 % bands were the Squirrel's polygon shares (CONTRACT.md:57) and are
   dropped.
3. **Paint by IDENTITY, not index**: every prefab authors `_domainReplacesMaterials = [GreenAccentVesselMaterial]` and `_shipGeometries = [mesh]`, never the FBX root (the Sparrow
   names the root and is never painted, `Sparrow.md:205`). WHY: the slot-1 default "is not a contract, it is a coincidence" (`Docs/VESSEL_CONSTRUCTION.md:345-349`).
   `ResolveDomainSlots` matches `sharedMaterials` by REFERENCE and only WARNS on no match (`VesselCustomization.cs:121-145`), so the list is authored in the SAME change that
   removes any `m_Materials` override on slot 0 (the Squirrel trap, section 9).
4. **The `.meta` remaps ALL FOUR names** (`externalObjects`), **and no `m_Materials` override sits on the hull renderer**: the intention is that the game's four `.mat` assets are
   the only materials a hull ever wears, on every surface; an unremapped slot imports the DCC material, which lacks `_ColorMultiplier` (Dolphin `N.001`, `Dolphin.md:81,169-170`).
   **Cross-surface consequence (Affects: Shombith's engine)**: Amoebius binds a model's materials through the PREFAB's `m_Materials`, not the `.meta`
   (`Port/src/CosmicShore.Content/Models/ModelMaterialUsage.cs:26-32`), so deleting the override makes the engine draw the importer's placeholder until the Port reads
   `externalObjects`; the two land together.
5. Code follow-up (proposal; owner Garrett, last author of `VesselAnimation.cs`; the Port twins are Shombith's, D37): flares and the painter resolve slots by material identity
   for every renderer kind and write `_ColorMultiplier` through `sharedMaterials` + `MaterialPropertyBlock` (`ScarabAnimation.cs:552-556` is the reference).

## 4. Four element shape keys

1. **Names** `Charge`, `Mass`, `Space`, `Time`, in that order (= `MorphElements`, `VesselElementalMorphConfigSO.cs:40-41`), on the one skinned mesh, no deformer prefix, no
   element token on any other shape (`TryResolveElement`, `:60-98`; case-insensitive, so the enum spelling is the pick). WHY the order is a binding: the runtime resolves by NAME
   (`CollectElementShapes`, `VesselAnimation.cs:499-525`), but the fusion bakes key by shape INDEX (rule 2.6), so a reorder is a re-bake.
2. **One frame per shape, last-frame weight 100, per-vertex normals, nothing below the basis**: the basis IS level 0 and the renderer weight equals level x 10
   (`VesselAnimation.cs:511-513`; `VesselElementalMorphConfigSO.cs:35-37,47-48`). Zero blend-shape curves in any take (`LateUpdate` is the single writer,
   `VesselAnimation.cs:484-491`).
3. **Magnitude**: farthest-vertex travel 4-12 % of the mesh AABB diagonal measured OFFLINE (`fbx_probe`), floor 3 %. WHY: the in-editor audit divides by root-bone-space bounds,
   1.283x smaller on the Dolphin (`Docs/VESSEL_CONSTRUCTION.md:298-301`), and its INERT line is 0.1 % (`VesselElementalMorphAuditor.cs:55`; SKILL.md:92 is corrected in this
   change); a shape that moves the whole mesh reads as a scale (LESSONS 20). The decisive gate is the chase tile: a pilot names the element from the silhouette.
4. **What each expresses** (`Docs/ElementalAbilitySystem/ARCHITECTURE.md:52-53`): **Space** = reach: extremities extend along their own axes, nothing thickens. **Time** = rate:
   sharpen and sweep, never bigger. **Charge** = threat: the WEAPON surfaces grow. **Mass** = volume: the body fattens about its centre. The part each one grows is the part that
   element's ability lives on (`python3 Tools/Build/element_ability_table.py <Vessel>`), so the morph names what grew.
5. **What a morph may NOT do**: leave the fixed hit box and skimmer (`Manta.md:260`: "morphed geometry must stay inside the intent of the fixed collider boxes"), move a `.mouth`
   bone's lip (jets are pinned, rule 2.5), or move the camera anchor; morph writes mesh / localPosition, puppetry writes localRotation, no channel has two writers. A hull that
   stays procedural morphs through `IProceduralElementMorphSource` (`IProceduralElementMorphSource.cs:17-26`) and says so.

## 5. Puppetry: one driver, roles first, takes optional

1. **OPEN DECISION for Garrett (last author of `VesselAnimation.cs`); recommendation (a).** Measured: 14 per-vessel `VesselAnimation` subclasses (BrittleStar, Bufo, Butterfly,
   Dolphin, Manta, MantaAnimationContoller, Rhino, Riptide, Scarab, SingleStick, Sparrow, Stoat, Thresher, Urchin) and NO generic role-driven driver; the only code naming a
   grammar role is `RiptideAnimation.cs:307` (`wing.l`) and a test. B3 needs a new vessel playable with no code (`VISION.md:54`). **(a) one generic `VesselAnimation` drives
   section 2's roles by grammar** (pitch -> `nose` and `hull`, roll -> `wing.L/.R` opposed, yaw -> `wing.*` fore / aft, throttle -> `jet.*` and `wing.*` sweep; amplitudes in a
   config SO, calibrated on `RhinoAnimation`'s `yawAnimationScaler = 80`, `RhinoAnimation.cs:14`; 14-26 deg reads as dead, `.claude/skills/vessel/SKILL.md:339-351`): no
   per-vessel C# subclass, no per-vessel `.controller`, takes OPTIONAL. **(b) the Animator path** is the fleet's and roles exist only for FX mounts. Only (a) is usable from a
   non-programmer's recipe. The MODEL owes (a) the roles present and a symmetric cruise rest (rule 2.4); nothing else.
2. **The nine input takes** (`takes.STANDARD_POSES`): `idle`, `pitchup`, `pitchdown`, `yawleft`, `yawright`, `rollleft`, `rollright`, `speedup`, `speeddown`, exported
   `rig|<name>`, single-frame POSES keyed at frame 1 (`takes.pose_take`'s `frame=1`), every deforming bone keyed, no blend-shape curves; the `.meta` pins `rig|<name> -> clip id`.
   Required while an Animator vessel stays on (b); under (a) they are documentation and the studio's pose row.
3. **Meaning in the Unity chase view**: `pitchup` = nose RISES; `yawright` = nose to the pilot's right (+X); `rollright` = the `.R` wing DROPS; `speedup` = full-throttle
   streamline; `speeddown` = brake; `idle` = rest = cruise; a diagonal is the blend, never a take (the Sparrow's `Speed+*` set is legacy). Authoring in the `zup` Blender frame:
   pitch about world X, roll about Y, yaw about Z (`takes.world_to_bone`; CONTRACT.md:109-110); nose-up = negative about X; confirm yaw and roll signs on the first pose row. The
   largest part deflects >= 20-30 deg.
4. **Ability takes are named by the INPUT they answer**, not by one hull's hardware (the first draft's `bay.L/.R`, `charge`, `fire` were the Sparrow's bays and guns renamed):
   `boost` (loops; `InputEvents.FullSpeedStraightAction`, `InputEvents.cs:7`) and `ability.<1|2|3>.start / .loop / .end` (`Button1-3Action`, `:13-15`; the fleet contract is four
   abilities on four inputs, `Docs/ElementalAbilitySystem/ARCHITECTURE.md:48-49`), additive layer, no shape curves. A vessel whose abilities are FX-only needs NONE.
5. **Sign convention at the driver** (proposal; owner Garrett): flight negates pitch on BOTH input paths (`DualStickMix.cs:46`, `VesselTransformer.cs:924-926`;
   `SingleStickVesselTransformer.cs:27` `-EasedLeftJoystickPosition.y`), but the PUPPETRY receives two signs: `VesselAnimation.cs:33` passes raw `EasedLeftJoystickPosition.y`,
   `:34` the already-negated `YSum`; the Serpent / Sparrow / Manta trees absorb it by placing `pitchup` at (0,-1) where the Squirrel's sits at (0,+1);
   `MantaAnimationContoller.cs:26-29` doubles and negates while its `Idle` does not (`:42`). `Idle` is raised during play only by touch (`TouchInputStrategy.cs:218`);
   `InputStatus.cs:276` (its reset) is the other writer; every strategy clears it (`BaseInputStrategy.cs:55`, `TouchInputStrategy.cs:241`). The convention normalises ONCE in
   `VesselAnimation.Update`: pitch +1 = nose up, yaw +1 = nose right, **roll +1 = bank LEFT (the `.L` wing dips)**, as flown (`YDiff` +1 turns about `transform.forward`,
   `VesselTransformer.cs:940-942`; left-handed, inference, check in the editor) and as the Squirrel graph places `rollleft` at Roll +1 (`SquirrelAnimatorController
   1.controller:173-176` = `squirrelanim_rollleft`, meta :140); throttle 0 = cruise; no `Idle` branch. One-thumb classes (`IsSingleStickControls`) receive roll 0 and throttle 0
   today (`:33`).

## 6. Sizes and cameras: derived from the hull, for a NEW vessel

The recipe carries the hull's **length nose->tail in metres** (section 8); everything else derives. **|followOffset.z| = length / 0.3** (inference: the five hand-tuned hulls sit
at length / |camera| 0.23-0.38: Squirrel 0.38, Dolphin 0.27, Urchin 0.27, Rhino 0.24-0.29, Sparrow 0.23; the Serpent's 0.05 is the disagreement its docs name,
`VESSEL_TAIL_AND_JETS.md:437-449`), then tail z = -1.05x, ribbon `widthScale` = /20, plume scale = (0.6, 0.6, 0.13) x /20 (`:117-142,211-216`). WHY: a world-unit distance is a
silent dependency on every scale between asset and scene (`Docs/VESSEL_CONSTRUCTION.md:717-721`). The band is a DEFINITION for a new hull, a WARNING on a shipped one (gate 7).
Under the artifact-is-source rule (CLAUDE.md) the camera is a studio parameter in principle, but `Tools/Build/studio_to_unity.py` maps no camera today (grep: none).

| class (sticks) | camera z | hull length (m) | tail z | width x | source |
|---|---|---|---|---|---|
| Urchin (2) | -6.67 | ~1.8 | -7.0 | 0.33 | CONTRACT.md:132; TAIL:227 |
| Squirrel (2) | -17 | 6.5 | authored pair (+-4, 0, -12), kept | 0.85 | CONTRACT.md:128; TAIL:214-216,225 |
| Dolphin (2) | -20 | 5.3 | -21 | 1.0 | CONTRACT.md:129; TAIL:224 |
| Manta, Falcon, Shrike, Termite (2 / 1) | -30 | open (two coincident hulls) | -31.5 | 1.5 | TAIL:231,233 |
| Grizzly (1) | -60 (`GrizzlyCameraSettingsSO.asset:16`, guid 7b995533 on `Grizzly.prefab`; TAIL:229 "-30, no SO" is stale, a doc fix) | wedge placeholder | -63 | 3.0 | this session |
| Sparrow (1), Scarab (1) | -50 | 11.5 | -52.5 | 2.5 | CONTRACT.md:131; TAIL:226,230 |
| Rhino (2) | -70 (the asset; TAIL:228's -120 is stale) | 16.8 fuselage, 20.4 composite | re-derive | 6.0 | `Rhino.md:192` |
| Serpent (1) | -250 | 11.6 | -262.5 | 12.5 | CONTRACT.md:130; TAIL:232 |

## 7. Derived art and the web form: rendered from the model, never hand-painted (proposal)

Every picture of a vessel the game or the studio shows is RENDERED from the model by `render.py` in the same build, so a new hull gets them for free: the card silhouette
(`SO_Vessel.CardSilohoutteActive / Inactive`, `SO_Vessel.cs:48-50`; `Assets/_Graphics/CardImages/<Vessel>_silhouette.png`, present for seven hulls and none for Stoat, Scarab,
Thresher, Butterfly, BrittleStar, Goby, Angler), the icons and previews (`SO_Vessel.cs:42-47`; the Serpent's HUD still binds `*-PLACEHOLDER.png`, `Serpent.md:209`), and the
studio card thumbnail (`Docs/Studios/VesselStudio/previews/<id>.png`, `studios.json:51`, baked by `.claude/skills/vessel-studio/bake_previews.cjs`). **The build also writes the
model's web form** (glTF/GLB, or the baked chase render until a loader lands) from the same spec, so the studio's stage and hub card show the real hull (rule 0.4). Gate 11.

## 8. What the recipe holds (the hull section of `vessels/<name>.json`, B3; proposal)

Length nose->tail in metres (rule 6); the roles present from section 2's table (so the generic driver and the FX wirer know what exists); the Domain area share; which ability
slots carry takes (rule 5.4), or none; the file path and the web form's path (rule 7); the budget it was built under (rule 1.4). Slots and shapes are fixed. B2's presets and dice
fill exactly these fields.

## 9. Legacy bindings per shipped vessel

Each row: what still differs from sections 1-8, and the step that retires it. An in-place overwrite today keeps every binding (same guid, names, rest poses, frame); nothing in
this table is a rule for a new hull. Owners from `who_works_where.py`, 2026-10-17.

> **Since these rows were measured (2026-10-17), on `ccr-d93a6dfb-ugbxng`, 2026-10-18:** the Urchin and Rhino
> prefabs draw their rigs (the placeholders' parts stay, and the bones follow them). The Manta family's renderer
> arrays are in FBX order `[Domain, Body, Window, Engine]` with the live renderer domain-painted and no Lime.
> The Rhino's meta paints `accent.005` as the domain and `N.005` as Engine. The Scarab, Shrike, Stoat, Thresher,
> Brittle Star, Angler and Butterfly draw their own FBX models (`Docs/VesselModels/README.md`). The colour rows:
> `Docs/VesselModels/COLOR.md` section 3. The rows below record what each prefab bound before that; re-measure
> before acting on one.

| vessel (dossier) | differs from the convention (why the prefab binds it) | migration step |
|---|---|---|
| Squirrel (`Squirrel.md`) | names `a_SquirrelShipMesh_nearfinal`, `a_Squirrel_Armature`, `bbone_*` / `abone_MAIN_Cockpit`; armature R(71.9,-180,0); takes `a_Squirrel_Armature\|squirrelanim_*`; meta remaps Body / Domain / Window, no Engine (`…Test1.fbx.meta:6-21`); the prefab overrides [0] `RedAccentVesselMaterial` (guid 5e4677e2), [1] `BlueBaseOpaqueVesselMaterial` (f5a1f1d4), [3] `EngineVesselMaterial` (0c127fff) (`Squirrel.prefab:3912-3926`); `_domainReplacesMaterials` empty (:1165); driver x2 / negate against a halving top tree | slots and shapes already canonical. In ONE change, in this order: delete the [0] override, author `_domainReplacesMaterials = [GreenAccentVesselMaterial]`, add the `Engine` meta row, delete the [3] override (ask Garrett about the opaque-body [1]); shared graph + override controller or driver (a); rename nodes and takes only on a re-wire |
| Dolphin (`Dolphin.md`) | `yup` frame, roots R(-90) S(100); slots `accent.001 / BASE.001 / windsheild.001 / N.001`, `N.001` unremapped so `FlareEngine` is inert; lowercase shapes; window 4 polys; takes `dolphin\|<pose>_dolphin`; bones `jetT/jetm/jetB`, nozzles `jetint/jetinm/jetinb`, `jaw.u/jaw.b`; the driver composes the chassis motion (`RiptideAnimation.cs:128-142`) and names the legacy bones first (:306-321) | identity list already authored. Re-wire: rename slots + four meta rows, re-bake the four `Dolphin_*_HullFusionBake` assets, `.mouth` names on the six nozzle bones (`VesselJet.mountBone` in `Dolphin.prefab`), `hull` parenting, `zup` with colliders re-fitted, convention names first in `ResolveParts` |
| Serpent (`Serpent.md`) | `zup_identity`; slots `Body, Engine, Window, Domain` and no identity list, so the index path paints `Body`, 64 % of the area (:208); `Engine` unremapped (`Engine_2.mat` orphan); shapes ordered `Space, Mass, Time, Charge` (`specs/serpent.py:237`, tied to the `Serpent_*` bakes); takes `SerpentArmature\|Serpent{Up,Down,Left,Right,Idle,SpeedIncrease}Anim`, pitch placed inverted, no roll / throttle tree; the static `serpent.fbx` (16 meshes, FBX 7700) still in the prefab (`Docs/VESSEL_CONSTRUCTION.md:61-68`); a 250 camera on an 11.6 hull | now: author `_domainReplacesMaterials` on `Serpent.prefab`. Re-wire: canonical slots (the meta binds by name) + `Engine` row, shape order + re-bake, retire `serpent.fbx` into the skinned model, `zup`, rename takes, re-derive the camera (rule 6) |
| Sparrow (`Sparrow.md`) | armature S 0.2034 (the 180 Z is the preset, rule 1.2), mesh node moved 185 units; `b_Tail*` bones drive the NOSE blades; the spec ALREADY moves the six `b_Tail1-3.L/.R` jet hosts to the stern in place (`specs/sparrow.py:230-234`, `rig.move_bones`, `rest_compare --allow-moved`), relying on `VesselJet.mountBone` (`Sparrow.prefab:2096-2516`) to follow by name, which contradicts SKILL.md:82 "moved ones are not": Garrett to confirm the plumes point aft, then SKILL.md:82 is corrected; slots `Body, Domain, Engine, Window`; shapes `Space, Charge, Time, Mass` (`sparrow.py:350`, tied to the `Sparrow_*` bakes); `_shipGeometries` = the FBX root so the domain is NEVER painted (:205) and `FlareEngine` flashes the glass (:170); takes `Sparrow_Armature+Mesh\|*_SparrowAnim` incl. `Speed+*`; missile clips from `SparrowModel4`; Roll / Throttle tree with four empty motions | now: `_shipGeometries -> mesh node` + identity list. Re-wire: canonical slots + `Engine` row, shape order + re-bake, `jet.<n>.<side>.mouth` names, `ability.*` takes in the same file, drop `SparrowModel4` |
| Manta family (`Manta.md`; Falcon, Shrike, Termite are flat copies) | two coincident hulls (`mantis_shapekey_with_animations` + `Manta_shapekey_rigged`, OPEN, `Docs/VESSEL_CONSTRUCTION.md:135-153`); `yup_baked`, armature x100, wing colliders 0.004 u; slots `accent / BASE / windsheild / N`, `N -> LimeVesselMaterial` in the meta; the two SMRs are UNPACKED with inline `m_Materials` (`Manta.prefab:739,1541`, not overrides, so rule 3.4's override clause does not reach them) rotated one slot so the body wears the Window material (:83,:261); the two Animators split (:592 no controller, :1441 `MantaAnimatorController`); **`Falcon.prefab:1982 _shipGeometries: []`**, so `TryPassNullChecks` errors and the Falcon is never painted (`VesselCustomization.cs:154-157`); `MantaAnimation` mixes throttle into the wings' yaw term and roll / pitch into the roll term, negating roll on the fuselage (`MantaAnimation.cs:22-62`), authored GameObject refs, no `ResolveParts`; no jets | decide which hull stays, then RE-NEST the hull (not an override delete): identity list + fixed inline arrays on all four prefabs, `Engine` meta row, delete the inactive duplicate and its dead controller, driver (a), `.mouth` bones for jets, `zup` at unit scale (Affects: Garrett's fusion bakes, Shombith's #1067 area, the three copies) |
| Urchin (`Urchin.md`) | prefab draws `Urchan_Test.fbx` part-per-mesh with 13 authored part fields, no SMR (no flares), engine nodes at 1.75 scale; an orphan Animator with `MantaAnimatorController`; the rebuilt rig is `yup_baked`, slots `accent / BASE / windsheild / N` (`slot_reorder_from` declared, `specs/urchin.py:195`), empty meta, bones `gunM.*`, `jetT/jetB.*`, `wingconrot*`, `sheildconrot*` (`UrchinAnimation.cs:33-49` names them first) | editor rig swap at the decided `localScale 0.474905` (`Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md:227-247`): rename / remap the four materials, set `VesselAnimation.SkinnedMeshRenderer`, delete the orphan Animator, roles `gun.L/.R`, `jet.T/.B.<side>` + `.mouth`, unit-scale engine nodes, convention names first in the driver |
| Rhino (`Rhino.md`) | prefab draws `Rhino_Test.fbx`; the index path paints `BASE` = 80 % of the hull (:102-104), pods not in `_shipGeometries`, no SMR; eight body-nozzle jets measured off `fusalage`; the rebuilt rig is `yup`, slots `accent.005 / … / N.005`, empty meta, bones `wing1.*`, `jet.*` (`RhinoAnimation.cs:23-26` names them first); the wings held `(-1 + throttle) * 80` off rest at a centred stick (:41); camera -70 in the asset | editor rig swap at the decided `z -1.5545` (FOLLOWUP.md:227-247): canonical slots + meta rows + identity list, `wing.L/.R`, `wing.2.L/.R`, `jet.L/.R` + `.mouth`, `.mouth` bones for the eight nozzles, normalised throttle; read `cece/relaxed-hopper-r62hbd` (11 unmerged Rhino commits) first |
| Grizzly | draws `Vessel_Wedge_Scene (4).fbx` (guid e276abc8) part-per-mesh x8 on `bleeding-edge` AND on `grizzly-v3` (jtgjones, 14 commits: weapons and claw, not hull art); four `Grizzly_*` bakes keyed to it | real FBX under this convention; **check with jtgjones before touching `Grizzly.prefab`**; the wedge's jets on `Ship_Wedge_Jet_*` become `.mouth` bones |
| Goby (`cece/sharp-tesla-j0kd7z`, 23 commits) | nests the Squirrel's FBX (`Goby.prefab:4000`, 46 refs to guid 5db69771), SMR wired, `_domainReplacesMaterials` empty (:1166): every Squirrel row applies, and rule 0.5 (a hull family) is its real question | author the identity list now; hull decision with Garrett under rule 0.5 |
| Angler (`cece/zealous-feynman-po8k8d`, 12 commits) | `AnglerHull` is a `MeshFilter` with `m_Mesh: {fileID: 0}` driven by `StoatAnimation` (`Angler.prefab:348-378,885`), i.e. the Stoat's runtime `StoatHullForm` (inference from `StoatAnimation.cs:11,146`); `SkinnedMeshRenderer: {fileID: 0}` (:888), identity list empty (:1002) | real FBX under this convention, or `IProceduralElementMorphSource` if it stays procedural; identity list now |
| Scarab, Stoat, Thresher, BrittleStar (the Butterfly's was replaced 2026-10-18 by `Butterfly.fbx`, `specs/butterfly.py`, the worked example of a new hull under this convention) | C# runtime hulls judged "terrible even as placeholders" (SKILL.md section 0); MeshRenderer two-submesh hulls painting slot 1 (`ScarabHullBuilder.cs:30-34`); the Scarab's renderers-off Sparrow wrapper; `BufoAnimation` absolute `RotatePart` with roll = -yaw | real FBX under this convention; until then author `_domainReplacesMaterials` on each so identity is the only rule |

Fleet-wide bindings, not per vessel: `ApplyShipMaterial` SMR `materials[0]` vs MeshRenderer slot 1 (`VesselHelper.cs:156-182`) and `FlareEngine` / `FlareBody` fixed indices
(`VesselAnimation.cs:527-548`), with the same two assumptions in the Port (`Port/.../Animation/VesselAnimation.cs:129-148`, `Port/.../Vessel/VesselHelper.cs:123`; Affects:
Shombith's engine); `ModelMaterialUsage` binding through `m_Materials` (rule 3.4); the `VesselCustomization.cs:22-32` tooltip names three roles and no Engine; `materials.py`
`slot_names_for` (:50-61) is a second copy of the specs' `SLOTS` tuples (the contradiction at HEAD was fixed in this tree; delete it anyway: `GENERIC` (:29) is the one order,
`ALIASES` (:41-45) move to the legacy layer); every driver's candidate list names the legacy bone first (rule 2.3); two drivers nothing references (`DolphinAnimation.cs`,
`SingleStickAnimationController.cs`); 10-15 orphan trees per controller; `VESSEL_CONSTRUCTION.md:338-342` mixes file order and prefab-serialized order in one "submesh order"
column.

## 10. What the pipeline already enforces vs what still needs a gate

Enforced by `export.verify` (PIPELINE.md "The verify gate"): the spec's bones / armature / mesh node present; one skinned mesh; every slot owns polygons and >= `min_slot_share`
of the re-imported AREA; every spec'd shape present and travelling >= `min_travel`; every spec'd take present, a pose deflects what it authored, zero blend-shape curves;
GlobalSettings and `NODE_TRS` per preset (the armature only on the cm rigs); and, seeded by `build.py` from the ARTIST file, slot order / size / material colours unless the spec
declares `slot_reorder_from` / `size_ref_blender` / `material_colours_ref`. `build.py --check` is the drift gate. Nothing checks fixed node names, the grammar, `.mouth` children,
slot / shape names in order, take names, an amplitude, a budget or the camera band.

Needs a gate (none of these fails today):
1. Preset `zup` for a NEW hull: mesh node identity AND the armature row `((0,0,0), 1, (0,0,180), 1)` in `NODE_TRS` (today `None`); a legacy preset accepted only with
   `preset_legacy` + its section 9 step.
2. Node names `mesh`, `rig`, `hull`; bones match the grammar; `hull` the single root; each jet bone a `.mouth` child; `takes.mirror_pose` token-swaps `.L/.R`. `specs/_wedge.py`
   is rewritten to the convention and becomes this gate's negative control; `rig.py:4-8` says "convention names first, legacy names a binding".
3. Slot names exactly `Domain, Body, Window, Engine` in order, AND all four `.meta` `externalObjects` rows present: the meta is plain YAML, so this is a Python gate beside
   `build.py --check`, not an editor step (today Squirrel / Serpent / Sparrow remap three, Dolphin three, mantis four, the three rebuilt rigs `{}`).
4. Shapes exactly `Charge, Mass, Space, Time` in order, last-frame weight 100, normals, travel 3-12 % offline, no element token elsewhere.
5. Takes exactly `rig|<pose>` at frame 1, every bone keyed, largest deflection >= 20 deg; ability takes per rule 5.4.
6. Budget: <= 14k tris, <= 42 bones, one SMR, exactly four shapes, zero n-gons (rule 1.4).
7. Hull length / |camera| outside 0.23-0.38: a WARNING (rule 6), a definition for a new hull.
8. Morph limits: every shape's full-weight hull inside the collider box and skimmer radius; `.mouth` bones and the camera anchor unmoved (4.5).
9. Prefab side, in the editor (`FrogletTools > Vessels > Audits`): `_shipGeometries` names the renderer's GameObject; **the hull's slot 0 WEARS a listed
   `_domainReplacesMaterials` entry after import** (not merely "list authored"); no `m_Materials` override on the hull renderer; every `VesselJet.mountBone` resolves to a
   `.mouth` bone at (0,0,0); fusion bakes fresh.
10. Driver side: one pitch sign at the dispatch, roll +1 = bank left, no touch-only `Idle` path (rule 5.5); or driver (a) lands.
11. Derived art: silhouette, previews, studio thumbnail and the web form newer than the model (rule 7).
12. The hovering assembly (section 0.5): `assembly_report.py <fbx> --floating <the spec's floating parts>` exits 1 when a part
    touches a neighbour (under 0.4 % of the diagonal) or drifts off (over 10 %). A tool today (`--self-test` holds its
    controls; the first Orca fails it on six parts), not yet wired into `export.verify`.

Every new gate is watched failing on a negative control before its green counts (CLAUDE.md's generator rule; `Docs/VESSEL_CONSTRUCTION.md:1187` is the measure-not-label rule, not
this one).
