# RULES — Unity import and prefab binding a replacement vessel FBX must obey

> Measured rules (Unity import and prefab binding), written by an independent reader on 2026-10-17 from the artist files and the prefabs as they
> were before the `/vessel-model` rebuild. They describe what TODAY's prefabs, metas and drivers bind, so an in-place
> replacement lands without the editor; they are not the convention. Where a rule here says "keep X as shipped" and
> `.claude/skills/vessel-model/references/FLEET_CONVENTION.md` says otherwise, the convention is the intention and the
> rule is a legacy binding with a migration step there (Garrett, 2026-10-17: the shipped vessels are works in progress).
> Paths under `<scratchpad>/` were the reader's temporary files; the commands beside them regenerate the numbers.


Measured 2026-10-17 on `ccr-d93a6dfb-ugbxng` (bleeding-edge merged). **Provenance matters here**: `git status` shows seven
files under `Assets/_Models/Vessel Models/` MODIFIED and uncommitted (Squirrel, dolphin_shapekey, SparrowModel1, SerpentExport,
Manta_shapekey_rigged, rhino_shapekey, urchan_shapekey — mtimes 16:47-16:50, rebuilt by `/vessel-model`). The pre-run probe
outputs (`scratchpad/probe/*.txt`, 15:11-15:58) are byte-identical to a probe of `HEAD` (`cmp` on the Squirrel: identical) and
NOT to the working tree. Below, **HEAD** = the committed artist file (what every prefab, meta and controller was authored
against), **WT** = the rebuilt working-tree file. HEAD copies: `scratchpad/head_fbx/*.fbx`; probes `scratchpad/probe_head/`
and `scratchpad/probe_wt/`; Blender measurements `scratchpad/bpy/measure_head.json`, `measure_wt.json`; round-trip files
`scratchpad/bpy/rt/`. Where a doc and an asset disagree the asset is quoted and the disagreement named.

---

## (a) How each prefab binds its FBX

Two binding shapes exist in the fleet. Both resolve through `(guid of the .fbx.meta, fileID of a sub-object)`; the fileID is
what the importer derives from the sub-object's TYPE and NAME (`fileIdsGeneration: 2`, `internalIDToNameTable: []` in all
15 metas — `grep fileIdsGeneration "Assets/_Models/Vessel Models/"*.meta`).

### A. Nested PrefabInstance + stripped components (Dolphin, Squirrel, Serpent, Sparrow)

The vessel prefab contains a `--- !u!1001` PrefabInstance whose `m_SourcePrefab` is the FBX's guid, an `m_Modifications`
list (targets are FBX sub-object fileIDs) and `stripped` stubs (`m_CorrespondingSourceObject: {fileID: <source id>, guid: <fbx>}`)
for every FBX object something in the vessel prefab references.

| prefab | instance | source file (guid) | `m_TransformParent` | mods / source objs | stripped stubs (type → count) | what hangs off them |
|---|---|---|---|---|---|---|
| Dolphin | `&1477261064774053533` | `dolphin_shapekey_with_animations.fbx` (`3f1757f0…`) | `2273877253388787683` = `OrientationHandle` under root `Dolphin` | 12 / 3 | Transform 18, SkinnedMeshRenderer 1, GameObject 1 | 6 `VesselJet` nested instances parented to 6 bone Transforms (`-555540679654791657`, `151156721315266023`, `-1923869225315705251`, `1777507748522082473`, `-726818501163874150`, `6948007125661694782`); 16 `m_AddedGameObjects` under bone transforms; SMR `2712662977449985951` → `RiptideAnimation.SkinnedMeshRenderer` (+ `m_UpdateWhenOffscreen: 1` override); GO `6162015126245842793` → `VesselCustomization._shipGeometries[0]` |
| Squirrel | `&8410834229753696920` | `SquirrelVessel_CosmicShoresTest1.fbx` (`5db69771…`) | `5928382658856804067` = root `Squirrel` | 28 / 8 | Animator 1, GameObject 2, SMR 1, Transform 6 | Animator `5866666021909216657` → `MantaAnimationContoller.animator`; SMR `2144674613060260969` → `.SkinnedMeshRenderer`; mesh GO `-2358995200592801505` (renamed `a_SquirrelShipMesh`) carries the added BoxCollider `(4.12,3.11,0.59)` + `ImpactCollider` and is `_shipGeometries[0]`; GO `-5228419308709608677` gets `m_Layer 8` + a second added BoxCollider `(2.79,3.93,0.55)`; 4 `VesselJet` instances parented to bone Transforms `1995807316851797615`, `-6731502047082032827`, `-2637102738391143170`, `5223167302631927187`; `m_RootBone` → stub `8878808177416813356` (source `1120998102324046260`) |
| Serpent | `&6541039741583580943` | `SerpentExport.fbx` (`7fad38a2…`) | `5928382658856804067` = root `Serpent` | 182 / 29 | Transform 28, Animator 1, SMR 1, GameObject 2 | **23 bone nodes carry `m_LocalPosition`/`m_LocalRotation` overrides (the prefab POSES the rig)**; 24 MonoBehaviours of script guid `1b37cffab30b939469bac076fdd82c59` reference the bone stubs as `m_ConstrainedObject` / `m_Source` (field names of Animation Rigging `DampedTransform`; `com.unity.animation.rigging 1.3.0` is in `Packages/manifest.json:11`; the script is not in this checkout, no `Library/`); SMR `7146665307521698322` → `MantaAnimationContoller` + `CloakSeedWallActionExecutor.shipRenderer`; mesh GO `-1218854902558664461` gets BoxCollider `(4.5,3.28,2.96)` + `ImpactCollider`; root GO `919132149155446097` gets a MonoBehaviour `fff0960ef4ea6e04eac66b4a7fd2189d` (unresolved, package script) |
| Serpent (2nd) | `&8968102816825443992` | `serpent.fbx` (`db4a9f6d…`) | `6747390897382510016` | 39 / 18 | Transform 1 | **`m_IsActive: 0`** vestige at `(-878.5, -562.9, -7.4)`, 17 `m_Materials` overrides — inactive, draws nothing |
| Sparrow | `&3134011750752858212` | `SparrowModel1.fbx` (`151e3f03…`) | `4853044355858455827` = root `Sparrow` | 94 / 74 | SMR 1, GameObject 1, Transform 2, Animator 1 | 68 `m_Layer: 8` overrides on GOs; SMR `2692722512241470911` → `SparrowAnimationController.SkinnedMeshRenderer`; a second SMR target `5953559334840733477` takes `m_RootBone` → stub `5885559472194428261` (source `8850569687463079169`) + blend weights; the ROOT GO `919132149155446097` carries the added BoxCollider `center (0,0,-3.5) size (10.31,0.46,4.38)` + `ImpactCollider` and is `_shipGeometries[0]`; the 6 jets are parented to non-FBX transforms and mount on bones BY NAME (`VesselJet.mountBone` `b_Tail1..3 .L/.R`, `Docs/VESSEL_TAIL_AND_JETS.md:226,239-258`) |

Root overrides (wrap-tolerant parse of `m_Modifications`, target `-8679921383154817045`):
Dolphin pos 0 rot identity; Squirrel pos 0 rot identity (+`m_ConstrainProportionsScale`); **Serpent `m_LocalScale (2,2,2)`,
rotation q=(0,-1,0,-2.5e-7) = 180° about Y, `m_LocalEulerAnglesHint (0,-180,0)`**; Sparrow pos 0 rot identity. The FBX root
name is overridden (`m_Name` on `919132149155446097`): `dolphin_shapekey_with_animations`, `SquirrelShip_CosmicShoresTest1`,
`SerpentExport`, `serpent`, `SparrowModel1`.

**What each stripped id must correspond to (by class):**
- `!u!1` GameObject and `!u!4` Transform stubs → a NODE of the file (mesh node, armature, or bone). Four ids are the same in
  every file and in every prefab: root GameObject `919132149155446097`, root Transform `-8679921383154817045`, root Animator
  `5866666021909216657` (Squirrel, Serpent, Sparrow, Dolphin, serpent.fbx all use them) — Unity's synthetic `//RootNode` for any
  model, independent of file name. Every other id is per node NAME/path.
- `!u!137` SkinnedMeshRenderer stub → the SMR Unity creates on the node that owns a skinned Geometry: Dolphin `dolohin` (typo is
  the shipped name, `Docs/VesselModels/Dolphin.md:118`), Squirrel `a_SquirrelShipMesh_nearfinal`, Serpent `SerpentMesh`,
  Sparrow `a_Sparrow_Mesh(MedPoly+Shapekeys)` (probe hierarchies, `scratchpad/probe/*.txt` line 5/34/5/71).
- `!u!95` Animator stub → the root node's Animator (constant id above; the import adds one when `animationType 2`).
- `m_RootBone` objectReferences → a bone Transform stub: Squirrel source `1120998102324046260` (the meta pins
  `rootMotionBoneName: abone_MAIN_Cockpit`), Sparrow source `8850569687463079169`.
- Jet/collider parents → bone Transform stubs; Dolphin's six jets sit under six different bones — a replacement that renames or
  drops any of those bones strands the jet at the vessel root (`VESSEL_TAIL_AND_JETS.md:247-254`).

### B. Direct mesh references on hand-built GameObjects (Manta, Urchin, Rhino)

No PrefabInstance of the FBX exists; `MeshFilter.m_Mesh` / `SkinnedMeshRenderer.m_Mesh` carry `{fileID: <mesh id>, guid: <fbx>,
type: 3}` and the prefab owns its own `m_Materials`, bones and Animator.

| prefab | file (guid) | renderers | mesh ids | transform chain to root | own m_Materials / Animator |
|---|---|---|---|---|---|
| Manta | `mantis_shapekey_with_animations.fbx` (`7c324ef5…`) AND `Manta_shapekey_rigged.fbx` (`77a38b23…`) | 2 SMRs, both on a GO named `manta` | **the same id `322997229772504633` in both files** (both files' Model node is `manta`, geometry `Sphere.018`) | mantis: `manta` R(-90° X) under `mantis_shapekey_with_animations` scale 100 → `OrientationHandle` → `Manta`; rigged: identity chain | both `[BlueBase 54e78ce1, Screen 923d3665, GreenAccent 539a8c65, Lime 99f64c48]` while the FBX slot order is `accent, BASE, windsheild, N` (`Docs/VesselModels/Manta.md:83,279` "Material array rotated"); Animators: avatar `9000000@7c324ef5` + `MantaAnimatorController` (`f922b4dd`), root motion 1, culling 0; avatar `9000000@77a38b23`, no controller, root motion 1 |
| Urchin | `Urchan_Test.fbx` (`fee259a2…`) | 13 MeshFilter+MeshRenderer | 13 distinct ids (`-1309588489851300069` … `2959467708922500008`) on GOs `Body`, `ShroudBottomRight/Left`, `LeftGun`, `RightGun`, `JetTop/BottomLeft/Right`, `ShroudTop/BottomLeft/Right`, `ShroudLeft/Right` | shroud/jet/gun GOs at chain scale **1.75**, `Body` at 1.0, all under `OrientationHandle` → `Urchin` | per-renderer arrays; Animator `m_Avatar: 0` + `MantaAnimatorController`, root motion 0 |
| Rhino | `Rhino_Test.fbx` (`4a586f92…`) | 7 MeshFilter+MeshRenderer | 7 ids on GOs `Rhino_Test (1)`, `Wing front left/right`, `Wing back Left/right`, `engine left/right` | identity chain, wings at `(±3.9246, 0.4619, 1.5752)` | per-renderer arrays; **no Animator in the prefab** |

Consequence: in form B a renamed or re-typed node silently blanks the part (the `MeshFilter` reads "Missing", no error);
the materials array and every collider stay as authored (Rhino root BoxCollider `size (5.6182046, 15.309107, 16.815693)`,
`center (0, -1.1998999, -1.9743571)` = the `Circle.003` fuselage AABB `(-280.9,-885.4,-1038)..(280.9,645.5,643.3)` × 0.01 —
a 4-decimal match, so the collider was fitted to THIS mesh at THIS import scale).

### Domain / flare slot binding (what slot ORDER a replacement must keep)

- `VesselHelper.ApplyShipMaterial` (`VesselHelper.cs:156-181`): a **SkinnedMeshRenderer always gets the domain material in
  slot 0** (`materials[0] = shipMaterial`, the slot argument is ignored); a MeshRenderer gets it in `_domainMaterialSlot`
  (default 1, clamped). `ApplyShipMaterialToSlots` (`:128-154`) is used only when `_domainReplacesMaterials` is non-empty:
  it paints every slot whose AUTHORED shared material is in that list.
- Authored per prefab (`VesselCustomization` block): Dolphin `_domainReplacesMaterials=[GreenAccentVesselMaterial]`, slot 1;
  Urchin `[GreenAccent]`, slot 1, 13 geometries; Squirrel / Sparrow `[]`, slot 1 (so slot 0 of the SMR); Manta / Serpent /
  Rhino: field absent (default 1), `[]`; Manta's 7 `_shipGeometries` are bone GOs with no renderer → never painted
  (`Manta.md:28`).
- `VesselAnimation.FlareEngine` = `SkinnedMeshRenderer.materials[3]`, `FlareBody` = `materials[0]`
  (`VesselAnimation.cs:527-548`): slot 3 must be the engine, slot 0 the domain/accent.
- Measured slot order per file (probe `material slots (ByPolygon)`): Squirrel `Domain 1804, Body 4400, Window 186, Engine 440`;
  SparrowModel1 `Body 11216, Domain 1384, Engine 192, Window 200`; SerpentExport `Body 2032, Engine 144, Window 80, Domain 2030`;
  Dolphin `accent.001 3078, BASE.001 7492, windsheild.001 10, N.001 533`; Manta/mantis `accent 174, BASE 2057, windsheild 38,
  N 887`; Rhino_Test fuselage `accent 364, BASE 3804, windsheild 358, N 234`; Urchan_Test Body `Material.004 980, .009 337,
  .002 33`. So "slot 0 = domain" is true of the Squirrel and the Dolphin/Manta family; on the Sparrow and Serpent slot 0 is
  `Body` and that is where the domain colour lands as shipped (`Docs/UNITY_VERIFICATION_CHECKLIST.md:96-97`). The task's
  "FBX materials Domain/Body/Window/Engine" is the Squirrel's order only — corrected per vessel above.
- Prefab material overrides on the nested SMR: Squirrel `m_Materials[0]=RedAccentVesselMaterial (5e4677e2)`,
  `[1]=BlueBaseOpaqueVesselMaterial (f5a1f1d4)`, `[3]=EngineVesselMaterial (0c127fff)` (slot 2 inherits the meta remap);
  Dolphin, Serpent (live instance), Sparrow: none — they wear the meta's `externalObjects` remaps.

---

## (b) fileID stability — the rule and its evidence

1. **Same file path + same guid + same node NAME + same TYPE ⇒ same fileID.**
   - Root ids identical across every file (table above).
   - Mesh id `322997229772504633` is the SAME in `Manta_shapekey_rigged.fbx` (vertices in cm, bounds ±197.6) and
     `mantis_shapekey_with_animations.fbx` (vertices in m, bounds ±1.976): different guid, different content, same `manta`
     node → the id is a function of type + name only (`Manta.prefab:768,1570`).
   - The Squirrel splice (`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md:70-84`): two exports of the same file, grafted at the binary
     level; "Same path + GUID; the mesh fileID is a name-hash shared by both exports, and the `.meta` pins each clip's take
     name to an explicit internalID" — the nested instance, clips and puppetry kept binding.
   - `Sparrow Missile.fbx` subdivided in place 624 → 9,984 tris (`Docs/UNITY_VERIFICATION_CHECKLIST.md:5640-5646`): "The
     geometry keeps its name, so it keeps its Unity fileID; the file keeps its guid … the prefab and the .meta did not change";
     step 10 (`:5700-5704`) adds "It resolves by fileID, which `fileIdsGeneration: 2` derives from the mesh NAME — unchanged
     — but that is reasoning, not a test."
2. **The id is NOT derivable offline, and cannot be borrowed.** `.claude/skills/asset-surgery/SKILL.md:389-396`: MD4 over
   name/class, both byte orders, all offsets, seven known meshes → zero hits; renaming a node in ANOTHER file to match a
   name whose id a different FBX records (the two manta files made it look safe) was tried on the omni crystal's triangle
   shells (2026-10) and "Unity did not reproduce the id and the shells rendered nothing". Reproduced here: MD4 (verified
   against RFC vectors) / MD5 / SHA-1 / SHA-256 over `Type:<T>-><name>0` and 190 variants (separators, suffixes, `//RootNode`
   forms, UTF-8 and UTF-16LE, int64 at byte 0/4/8, both endiannesses) against eight known ids (`919132149155446097`,
   `-8679921383154817045`, `5866666021909216657`, `322997229772504633`, `2132225776441424335`, `2144674613060260969`,
   `2692722512241470911`, `-2358995200592801505`): **zero hits**.
3. **Safe:** overwrite the SAME path (same guid) with a file whose node names, node types (Mesh / Null / LimbNode), skinned
   mesh node, armature and bone names, Geometry names and take names are unchanged. Everything in (a) keeps binding and the
   meta's clip and material entries keep resolving. The seven dossiers all recommend this path
   (`Docs/VesselModels/{Squirrel,Dolphin,Sparrow,Serpent,Manta,Urchin,Rhino}.md`, "Wiring path — in-place overwrite").
4. **Not safe:** a NEW file (new guid — every `guid:` in every prefab, controller and bake must be re-pointed and every
   fileID re-learned from a Unity import), or a RENAMED node (the stub's `m_CorrespondingSourceObject` names an id the new
   import never creates). Changing a node's type (a `Null` armature becoming a `LimbNode`, a mesh node becoming a bone) is
   the same failure under the same name.
5. **What a stale stripped reference does.** Unity's prefab merge keeps the vessel prefab loading: a modification or added
   object whose target id no longer exists is carried as an "unused override" (invisible unless you open the Overrides
   dropdown), and a stripped stub whose source is gone resolves to a missing object → the field that pointed at it reads
   null. The code paths then do nothing rather than throw: `FlareEngine`/`FlareBody` are `if (SkinnedMeshRenderer)` guarded
   (`VesselAnimation.cs:529-548`), `ResolvePart` returns null and `ReportUnresolvedParts` warns while "limbs degrade frozen
   rather than NRE" (`CONTRACT.md §7`), `VesselJet.ResolveMountBone` logs and "the jet stays where the prefab put it"
   (`VESSEL_TAIL_AND_JETS.md:253-254`), a direct `MeshFilter` draws nothing (`Urchin.md:214`). CLAUDE.md § Shared prefabs:
   "A scene override always beats the prefab … a nulled reference fails as 'the feature quietly does nothing', not as an error."
6. **The Serpent corollary:** its prefab overrides position/rotation on 23 bones. Keep the names and the OVERRIDE wins over
   the new file's rest pose (the prefab re-poses whatever rig arrives); change the names and 23 overrides go unused while the
   24 constraint components lose `m_ConstrainedObject`/`m_Source` targets. `Docs/VesselModels/Serpent.md:247` calls these
   "the 24 rest-pose overrides" and rates the in-place risk moderate for exactly this reason.

---

## (c) The .meta import settings (15 metas read; values identical unless listed)

Common to all 15: `materialImportMode 2` (ImportViaMaterialDescription), `materialName 0`, `materialSearch 1`,
`materialLocation 1`, `animationType 2` (Generic), `importAnimation 1`, `importBlendShapes 1`,
`blendShapeNormalImportMode 1` (Import), `normalImportMode 0` (Import), `normalCalculationMode 4`,
`legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes 0` (**no meta has a `legacyBlendShapeNormals` key** —
`grep -c legacyBlendShapeNormals` = 0 on all 15), `optimizeGameObjects 0`, `optimizeBones 1`, `meshCompression 0`,
`weldVertices 1`, `indexFormat 0` (Auto), `bakeAxisConversion 0`, `useFileUnits 1`, `useFileScale 1`, `resampleCurves 1`,
`animationCompression 1`, `importCameras 1`, `importLights 1`, `fileIdsGeneration 2`, `internalIDToNameTable []`.

Per file:

| meta | guid | globalScale | isReadable | avatarSetup | rootMotionBoneName / motionNodeName | removeConstantScaleCurves | clipAnimations | externalObjects |
|---|---|---|---|---|---|---|---|---|
| SquirrelVessel_CosmicShoresTest1 | 5db69771… | 1 | 1 | 1 | `abone_MAIN_Cockpit` / – | 0 | 9 | Body→BlueBase `54e78ce1`, Domain→GreenAccent `539a8c65`, Window→Screen `923d3665` (no Engine) |
| SparrowModel1 | 151e3f03… | 1 | 1 | 1 | – | 0 | 11 | same three |
| SparrowModel4 | ebe998f7… | **100** | 0 | 1 | – | 0 | 13 | same three |
| SerpentExport | 7fad38a2… | 1 | 1 | 1 | `HeadRootBone` / – | 0 | 6 | same three |
| dolphin_shapekey_with_animations | 3f1757f0… | 1 | 1 | 0 | – | 1 | [] | BASE.001→Blue, accent.001→GreenAccent, windsheild.001→Screen (N.001 unmapped) |
| mantis_shapekey_with_animations | 7c324ef5… | 1 | 1 | 1 | `root` / `Armature.002/root` | 1 | 9 | BASE→Blue, N→Lime `99f64c48`, accent→GreenAccent, windsheild→Screen |
| Manta_shapekey_rigged | 77a38b23… | 1 | 1 | 1 | `chassis` / – | 1 | [] | {} |
| Urchan_Test | fee259a2… | 1 | 1 | 0 | – | 1 | [] | {} |
| Rhino_Test | 4a586f92… | 1 | 1 | 0 | – | 1 | [] | {} |
| serpent | db4a9f6d… | 1 | 1 | 0 | – | 0 | [] | {} |
| Dolphin_Test / RhinoModel / rhino_shapekey / urchan_shapekey / Vessel_Wedge_Scene (4) | – | 1 | 0/0/0/0/1 | 0 | – | 1 | [] | {} |

Every `clipAnimations` entry: `firstFrame 0`, `lastFrame 1` (Squirrel / Sparrow1 / Serpent; mantis 19-20; SparrowModel4
"Missile Launch 1/2" 0-22), `wrapMode 0`, `loopTime 0`, `loopBlend* 0`, `curves []`, `maskType 3`, `internalID` explicit,
`takeName` = the file's take string verbatim (SparrowModel4's are the double-prefixed
`Sparrow_Armature+Mesh|Sparrow_Armature+Mesh|…` and the file's takes ARE double-prefixed, `probe/SparrowModel4.txt:96-109`).

**How clip fileIDs are pinned and consumed.** Each controller references motions as `{fileID: <internalID>, guid: <fbx>,
type: 3}` (wrap-tolerant grep of `m_Motion`): `SquirrelAnimatorController 1` → 9 ids, all present in the Squirrel meta's
`clipAnimations` (`squirrelanim_idle/pitchdown/pitchup/rollleft/rollright/speeddown/speedup/yawleft/yawright`);
`SerpentAnimController` → 4 of the 6 Serpent clips (`Up/Right/Left/Down`); `SparrowAnimatorController` → 10 SparrowModel1
clips + `Missile Launch 1/2` from SparrowModel4 (`2132225776441424335`, `3552259950087936227`); `MantaAnimatorController`
→ 8 mantis clips. Parameters: Squirrel `Blend, Pitch, Yaw, Roll, Throttle, x, y`; Serpent / Sparrow `Blend, pitch, yaw, roll,
throttle, Pitch, Yaw, Roll, Throttle` (+ `Boost` on Sparrow); Manta `Blend, Pitch, Yaw, Roll, Throttle` — driven by
`MantaAnimationContoller.cs:26-45` / `SparrowAnimationController.cs:73-103` (`SetFloat("Pitch"…)`, `SetBool("Boost")`,
`animator.Play(RightBayLaunchState…)`). All four controllers ALSO reference 8 clip ids in `urchan_shapekey_with_animations.fbx`
whose meta has `clipAnimations: []` — i.e. ids of Unity's DEFAULT per-take clips, which are therefore just as deterministic
(take-name derived) as pinned ones.

**NEW file beside an existing one** → Unity writes a fresh meta with defaults: `globalScale 1`, `useFileScale 1`,
`clipAnimations []` (one default clip per take, id derived from the take name — see the urchan evidence; enough for a
controller built against THEM, useless for the existing controllers, which name the OLD guid), `externalObjects {}` (every
slot wears the FBX-embedded material until remapped or overridden in the prefab), `isReadable 0` (the hull-fusion bakes and
the occlusion corridor read vertices — `Urchin.md:205` "isReadable 1 … needed"), `avatarSetup` not measurable here (the
shipped Generic-avatar files carry 1). So a new file costs: re-pointing every `guid:` in the prefab, the controller motions
(24-27 per controller), the `HullFusionBake.hullMesh` refs, and an editor import to learn every fileID.

**IN-PLACE overwrite** → the meta stays. `clipAnimations` binds by `takeName`: a take that disappears leaves its entry
pointing at nothing (the clip imports with no curves; the controller still references the id, so the state plays a static
pose — the Squirrel re-export of 2024-11-15 is the recorded case of silent breakage by take content,
`VESSEL_HUD_AND_ELEMENTS.md:71-74`); a take ADDED while the list is non-empty is not auto-listed — the in-repo state shows the
list can be shorter than the file: HEAD `SparrowModel1` has 13 takes (`probe/SparrowModel1.txt:86-108`) and 11 entries
(`Idle_SparrowAnim.006` and `a_Sparrow_Mesh(MedPoly+Shapekeys)|Idle_SparrowAnim.006` are not clips); SparrowModel4 has 16 takes
and 13 entries. Keep every take name verbatim (`<armature>|<action>` form, case and spacing included) and add a
`clipAnimations` entry (with a fresh `internalID`) only for a take a controller must reference.

---

## (d) Axis and scale conversion

**Measured file frames** (probe line 3, HEAD = WT for all seven): Squirrel `UnitScaleFactor 100, up=+2 front=+1 coord=-0`
(UpAxis Z+, FrontAxis Y+, CoordAxis X−, TimeMode 11); SparrowModel1 same with TimeMode 10; SerpentExport `100, up=+2 front=-1
coord=+0`; Dolphin rig, Manta_shapekey_rigged, mantis, Urchan_Test, Rhino_Test, serpent, rhino_shapekey, urchan_shapekey
`UnitScaleFactor 1, up=+1 front=+2 coord=+0`; SparrowModel4 `UnitScaleFactor 1` (meta `globalScale 100`).

**bpy round trip** (`scratchpad/bpy/roundtrip.py`, Blender 5.2 module): a 2 m cube with a cone NOSE on Blender −Y (tip at
y=−3), a fin on +Z, a wing on +X; exported five ways, probed, re-imported:

| export | GlobalSettings written | root node TRS in file | raw vertex bounds in file |
|---|---|---|---|
| `axis_forward='-Z', axis_up='Y'` (Blender default), `FBX_SCALE_NONE` | `up=+1 front=+2 coord=+0`, UnitScaleFactor 1 | `R=(-90,0,0) S=(100,100,100)` | `(-1,-3,-1)..(2.5,1,1.5)` — unchanged Blender coords, nose still at vertex y=−3 |
| `axis_forward='-Y', axis_up='Z'`, SCALE_NONE | `up=+2 front=+1 coord=-0` (= Squirrel / Sparrow) | `R=(0,0,180) S=100` | unchanged |
| `axis_forward='Y', axis_up='Z'`, SCALE_NONE | `up=+2 front=-1 coord=+0` (= SerpentExport) | `R=(0,0,0) S=100` | unchanged |
| default axes, `FBX_SCALE_UNITS` | `up=+1 front=+2 coord=+0`, **UnitScaleFactor 100** | `R=(-90,0,0) S=1` | unchanged |
| default axes, `FBX_SCALE_ALL` | same as UNITS | `R=(-90,0,0) S=1` | unchanged |

So Blender's exporter never rotates or scales vertex data; the axis choice lands on the root node's rotation plus the
`GlobalSettings` triple, and the unit choice is either `S=100` on root nodes (SCALE_NONE, UnitScaleFactor 1 — the Dolphin /
Manta / Rhino / Urchin convention) or `UnitScaleFactor 100` with unscaled nodes (SCALE_UNITS — the Squirrel / Sparrow /
Serpent convention). Re-import of all five gives the identical Blender scene (nose at −3 on Y, scale 1, rot 0): the three
presets are information-equivalent for Blender. The artist armatures carry the artist's own rotation on top: Squirrel
`a_Squirrel_Armature R=(71.89,-180,0)` = exporter `R_z(180)` × the artist's `X −108.1°` (Blender re-import shows the armature at
`[-108.1, 0, 0]`, `measure_head.json`); Sparrow mesh node `R=(0,0,180)` under an armature with **`S=0.2034`**
(`Sparrow.md:27`); SerpentExport identity.

**What Unity does with the file** (two in-repo measurements, no editor needed):
- Y-up file: `Docs/VESSEL_CONSTRUCTION.md §4.6.5` — "Unity's import of this rig is a conjugation by `0.01 · diag(−1, 1, 1)` —
  local translations ×0.01 and x-mirrored, rotations conjugated, local scales preserved" (fit residual 3.4e-5…5.8e-5 over
  twelve collider hosts). Confirmed independently on `Rhino_Test.fbx`: node `Wing front left` `T=(392.5, 46.19, 157.5)` cm
  → prefab GO `Wing front left` `m_LocalPosition (-3.9246707, 0.46191883, 1.575218)`; `Wing front right` `T=(-392.5, …)` →
  `(+3.9246545, …)`. **Unity = (−x, y, z) × fileScale**, fileScale = `UnitScaleFactor/100 × globalScale`.
- Z-up file: `Docs/VESSEL_TAIL_AND_JETS.md:304` "Unity's own Z-up→Y-up conversion `(−x, z, −y)`" (the Sparrow, pinned three
  independent ways: gun bone on the authored muzzle, SkyBurst gun point at `z +9.9` just ahead of the hull's `+z` extremity
  `+7.57`, camera `followOffset.z = −50`).
- Combined with the exporter table: a **default export** ( `('Y','-Z')` ) puts the Blender −Y nose on FBX world +Z through the
  root `R_x(−90)` and Unity keeps Z → **nose on Unity +Z, Blender +Z top on Unity +Y, Blender +X (the vessel's left when
  facing −Y under Z-up) on Unity −X (its left)**. The Dolphin proves it: default-export file, every transform from the SMR to
  the prefab root identity (`OrientationHandle`, `Dolphin`: rot 0, pos 0, scale 1), `Dolphin.md:39` "Nose axis = FBX world +Z =
  Unity +Z" from the jaw clusters at z `[0.832, 2.827]` and nozzles at z `[−2.29, −1.89]`. A `('Z','-Y')` export instead
  carries `R_z(180)`: with `(−x, z, −y)` the Blender −Y nose lands on Unity −Z — which is why the artist Squirrel and Sparrow
  are built with the nose on **file +Y** (`Squirrel.md:56` "the cockpit glass and the FRONT engine pods sit at file +Y",
  `Sparrow.md:30,64` "Unity (x,y,z) = (−Fx, Fz, −Fy) = (blender x, blender z, blender y) … the artist built the nose at file +Y
  (the FBX FrontAxis)") and why the Sparrow's cockpit glass flies BEHIND its "tail" blades. A `('Z','Y')` export (SerpentExport)
  is identity in the file and the prefab compensates with the 180° Y root override.

**Rule for the model builder.**
- Replacement in place: reproduce the shipped file's `GlobalSettings` triple, `UnitScaleFactor`, root-node TRS and the raw
  vertex frame EXACTLY (the prefab's root override — Serpent 180°/×2 — the 23 posed Serpent bones, the Dolphin's 16 bone-parented
  GOs, every collider and jet offset were authored against that frame). `.claude/skills/vessel-model/references/CONTRACT.md:72-77`
  names the four presets (`zup`, `zup_identity`, `yup`, `yup_baked`) per file; the probe line 3 + hierarchy TRS is the check.
- New file: model nose on Blender −Y, top +Z, pilot's left +X; export `axis_forward='-Z', axis_up='Y'` (either scale option);
  the nose arrives on Unity +Z with no prefab rotation (Dolphin evidence).

**UnitScaleFactor 100 vs 1.** Unity's fileScale is `UnitScaleFactor/100`: Squirrel (100, globalScale 1) → ×1.0, raw bounds
`(±2.248, −4.558..1.893, −0.6845..0.77)` are already Unity units; Dolphin / Manta / Urchin / Rhino (1, globalScale 1) → ×0.01
with the ×100 living on the root nodes (`S=(100,100,100)`, §4.6.4 table) or baked into cm vertices (Manta_shapekey_rigged
`±197.6`, Rhino_Test, Urchan_Test). SparrowModel4 (UnitScaleFactor 1, `globalScale 100`) and SparrowModel1 (100, 1) hit the same
product — `VESSEL_HUD_AND_ELEMENTS.md`/asset-surgery 4.8 note the equality; `measure_head.json` shows SparrowModel4 at
`0.125 × 0.122 × 0.018 m` before `globalScale 100` vs SparrowModel1 `12.499 × 12.229 × 1.776 m`, ratio 100.0.

**Hull size in Unity units, HEAD (measured, Blender world AABB = fileScale × node transforms; then × prefab chain):**

| vessel | Blender world size (m) x,y,z | prefab chain scale | hull in Unity units (x width, y height, z length) | nose proxy |
|---|---|---|---|---|
| Squirrel | 4.496 × 6.688 × 1.131 (raw-frame numbers `Squirrel.md:45`: width 4.496, length 6.451, height 1.455) | 1 | **4.50 × 1.45 × 6.45**, nose `z +1.89`, tail tip `z −4.56` | window centroid raw `(0, +0.591, +0.130)` |
| Dolphin | 8.209 × 5.846 × 2.022 | 1 (OrientationHandle, Dolphin) | **8.21 × 2.02 × 5.85** (wingspan 8.2) | window Blender y −0.492 → Unity +z |
| Sparrow | 12.499 × 12.229 × 1.776 (raw 61.46 × 60.13 × 8.73 under armature S 0.2034) | 1 | **12.5 × 1.78 × 12.2**; root BoxCollider `(10.31, 0.46, 4.38)` at `z −3.5` | nose = Blender +y (file +Y) |
| Serpent | 8.404 × 11.591 × 11.322 | root override **×2** | **16.8 × 22.6 × 23.2** | window Blender y −0.52 at the +Y end of a −10.1..1.49 hull |
| Manta | 3.951 × 2.275 × 0.585 (mantis) / 5.523 × 3.221 × 0.815 (rigged, incl. the 90° X node) | 100 (mantis GO) / 1 | **3.95 × 0.585 × 2.28** both files (`measure`: fileScale 0.01 × chain 100 = raw 3.952) | canopy at z `[−0.28, +0.38]` (`Manta.md:52`) |
| Urchin | Body sphere 0.432 × 0.415 × 0.406 (cm data × 0.01); parts at ±0.2 | 1 (Body), **1.75** (shrouds/jets/guns) | body **≈0.41** diameter (Sphere collider radius `0.2072551`), parts × 1.75 | canopy = file +Z cap (`Urchin.md:62`) |
| Rhino | 11.59 × 16.816 × 15.309 | 1 | **5.62 × 15.31 × 16.82** (fuselage box collider, 4-decimal match); wings to ±3.92 | window Blender y +0.901 |

---

## (e) Blend-shape import rules

- **Label resolution** (`Assets/_Scripts/ScriptableObjects/VesselElementalMorphConfigSO.cs:40-98`): `MorphElements =
  {Charge, Mass, Space, Time}`; take the text after the LAST `.` (so `blendShape1.charge` → `charge`); if it equals an element
  name case-insensitively → bound. Otherwise split the WHOLE name on `_ . - ' '` and bind iff exactly ONE token equals an
  element (`mass_hull` binds, `massive_jaw` does not, `mass_time` is ambiguous → ignored). Shipped names: Squirrel
  `Time/Mass/Space/Charge`, Dolphin `mass/charge/space/time`, Manta `Mass/charge/space/time`, Serpent and Sparrow
  `Space/Mass/Time/Charge` (probe `shape` rows) — all bind by the first rule.
- **Discovery** (`VesselAnimation.CollectElementShapes`, `.cs:499-523`): every `SkinnedMeshRenderer` under the vessel root
  (inactive included), every blend shape of `sharedMesh`; `FullWeight` = the last frame's weight (falls back to 100).
  No per-prefab flag; a `MeshRenderer` hull never morphs (Urchin, Rhino — `CONTRACT.md §7`, `VESSEL_CONSTRUCTION.md §1`).
- **Audit threshold** (`Assets/_Scripts/Editor/VesselElementalMorphAuditor.cs:55,145-184`): `MinShapeTravelFraction = 0.001f`;
  `Travel()` = farthest vertex delta of the last frame (`GetBlendShapeFrameVertices`) ÷ `mesh.bounds.size.magnitude`. For a
  skinned mesh `mesh.bounds` is the root-bone-space culling volume, so the editor number is a per-rig constant below the
  offline one — Dolphin ratio **1.283** on all four shapes (`VESSEL_CONSTRUCTION.md:290-323`); `fbx_probe.py:273` divides
  by the raw vertex AABB diagonal instead. Real shipped shapes: 2.46-17.94 % of the diagonal; a labelled-but-empty shape
  (HEAD Rhino/Urchin rigs: four shapes at 0.0000 %) reads INERT.
- **Single writer**: `VesselAnimation.LateUpdate` (`.cs:484-491`) writes the cached weights to the renderers every frame
  after the Animator has written its bound curves; tweens only touch the cache (`VESSEL_HUD_AND_ELEMENTS.md:85-90`,
  `CONTRACT.md §7`). Therefore an FBX may carry NO blend-shape animation curves: Blender writes `Key|…` channels when it bakes
  shape-key animation, and the Animator would re-stomp the weights every frame before LateUpdate corrects them.
  **Measured (probe `takes`, "N blend-shape curves")**: HEAD Dolphin 10/10 takes carry 4 curves each; SerpentExport 6/6 × 4;
  SparrowModel1 13/13 × 4; mantis 9/9 × 4; rhino_shapekey 9/9 × 4; urchan_shapekey 11/11 × 4; **Squirrel 0** (the splice left
  the donor's constant-zero residue out, `VESSEL_HUD_AND_ELEMENTS.md:78-79`). The WT rebuilds carry **0** on all seven. The
  Dolphin's curves are harmless today only because its Animator has no controller (no `m_Controller` override; `RiptideAnimation`
  drives bones directly); the Serpent's and Sparrow's play through their controllers and are being overwritten by LateUpdate
  every frame as the doc describes.
- **Normals**: metas import normals (`normalImportMode 0`) and shape normals (`blendShapeNormalImportMode 1`); every shipped
  shape row reports `normals=True`. A replacement must write per-shape normals or the morphed hull keeps base normals
  (shading seams at the extremes). No `legacyBlendShapeNormals` key exists in any meta.
- **Weights at rest**: the prefabs override `m_BlendShapeWeights.Array.data[0..3] = 0` on each SMR (Squirrel, Serpent,
  Sparrow); a file that ships non-zero shape weights is zeroed by the prefab.

---

## (f) Everything else the prefab needs without the editor

- `isReadable 1` on every file a prefab draws (Squirrel, Dolphin rig, SparrowModel1, SerpentExport, Manta ×2, Urchan_Test,
  Rhino_Test) — the `HullFusionBake` assets and `PrismOcclusionCorridor` read vertices (`Urchin.md:205`, `CONTRACT.md §7`);
  bakes store a vertex count + hash and go stale on any new mesh (`UNITY_VERIFICATION_CHECKLIST.md:92-94`).
- `meshCompression 0`, `indexFormat 0` (Auto: >65,535 vertices after `weldVertices 1` flips to 32-bit — HEAD Sparrow has
  13,426 file vertices, far below), `weldVertices 1` (Unity's vertex count ≠ the file's; do not compare counts across tools).
- `optimizeGameObjects 0`: every bone is a Transform in the hierarchy — required by the stripped bone stubs, the bone-parented
  jets/colliders, `ResolvePart` and the Serpent's constraint targets. `optimizeBones 1` only drops bones that skin nothing
  (Squirrel `bbone_WholeWing.L` skins 0 verts but parents the slats; the dossiers note the 16 Sparrow spoke bones likewise).
- Avatar: `animationType 2` (Generic). Squirrel / SerpentExport / SparrowModel1 / mantis / Manta_shapekey_rigged carry
  `avatarSetup 1` (avatar from this model, sub-asset `9000000`); the Manta prefab's two Animators reference exactly those
  (`{fileID: 9000000, guid: 7c324ef5…}` and `…77a38b23…`); the Squirrel/Serpent/Sparrow prefabs use the FBX's own Animator
  (stub `5866666021909216657`) whose avatar is the import's. Generic root-motion node names are pinned in the metas:
  `abone_MAIN_Cockpit` (Squirrel), `HeadRootBone` (Serpent), `chassis` (Manta rigged), `root` + `Armature.002/root`
  (mantis) — a replacement must keep those bone names or the avatar's root node dangles.
- Root motion: `m_ApplyRootMotion` 0 on Squirrel and Sparrow, **1** on Serpent (override) and on both Manta Animators, 0 on
  Urchin (whose Animator has `m_Avatar 0` and the Manta controller — vestigial). `m_CullingMode 0` (AlwaysAnimate) on
  Squirrel/Serpent/Sparrow/mantis Animators; Dolphin SMR `m_UpdateWhenOffscreen 1`.
- Geometry the importer DISCARDS (`VESSEL_CONSTRUCTION.md §4.7`): a polygon with a zero-length edge is "self-intersecting"
  and dropped (twelve 13-gons on the HEAD Dolphin); zero-area triangles are kept. Ship quads/tris with no repeated corner.
- Material slots: keep the file's slot ORDER and material NAMES (`Body/Domain/Window` are what the `externalObjects`
  remaps match; the `Engine` slot has no remap anywhere, so the Squirrel prefab overrides it to `EngineVesselMaterial` and the
  Serpent/Sparrow wear the embedded FBX material there); on the Dolphin/Manta family the names are `BASE/accent/windsheild/N`.
  Every slot needs polygons — `ApplyShipMaterial` and `Flare*` index the array positionally.

---

## WT (rebuilt) vs HEAD (artist) — measured differences a reviewer must know (the asset is the record)

All names, node types, slot names, take names, GlobalSettings and root TRS are preserved in the seven WT files (probe
diffs). What changed (Blender world AABB, `measure_wt.json` vs `measure_head.json`; probe `material slots` / `shape` / `takes`):

| file | HEAD size (m) | WT size (m) | WT centre / node facts | slots polys HEAD → WT | shapes HEAD → WT | takes |
|---|---|---|---|---|---|---|
| Squirrel | 4.496 × 6.688 × 1.131 | 4.46 × 6.386 × 1.662 (47 % taller) | window centroid y −0.592 → −1.074 | 1804/4400/186/440 → 2153/5746/105/444 | 6.4/2.5/4.6/11.1 % → 7.0/3.4/8.3/5.9 % | 9 → 9, 0 → 0 curves |
| Dolphin | 8.209 × 5.846 × 2.022 | **5.28 × 5.30 × 1.42 (36 % narrower)** | window y −0.492 → −0.084 | 3078/7492/10/533 → 555/2761/9/288 | 15.5/5.5/4.0/17.4 → 4.5/4.4/8.7/7.6 % | 10 → 10, 4 → 0 curves/take; moving nodes 24 → 9 |
| SparrowModel1 | 12.499 × 12.229 × 1.776 | **9.387 × 11.481 × 1.316** | **centre x +1.543 (HEAD 0); mesh node S=4.917 (HEAD 1.0)** | 11216/1384/192/200 → 6311/625/704/9 (Window 704, Engine 9) | 11.2/4.2/17.4/3.6 → 8.8/5.4/5.2/9.2 % | 13 → 12 (the `a_Sparrow_Mesh…|Idle.006` take dropped), 4 → 0 curves |
| SerpentExport | 8.404 × 11.591 × 11.322 | 8.518 × 11.925 × 11.41 | window y −0.52 → +0.959 | 2032/144/80/2030 → 1699/255/95/343 | 11.8/4.7/14.4/16.7 → 14.9/4.7/8.3/15.5 % | 6 → 6, 4 → 0 curves |
| Manta_shapekey_rigged | 5.523 × 3.221 × 0.815 | **3.96 × 2.244 × 0.65** | — | 174/2057/38/887 → 330/1554/9/45 | 12.0/7.3/17.8/15.6 → 7.3/5.7/13.8/12.9 % | **0 → 9** (new takes in a file whose meta has `clipAnimations []` and whose prefab Animator has no controller) |
| rhino_shapekey | 25.197 × 15.777 × 19.927 | 22.965 × 19.973 × 12.841 | **centre x −4.013, z −2.239** (HEAD 0, 0.17) | 766/6094/358/494 → 388/2664/5/834 | 0/0/0/0 (EMPTY) → 8.5/4.3/7.8/7.3 % | 9 → 9 but renamed `rhino|rhino_pitchdown` → `rhino|pitchdown` etc. (file not drawn by any prefab) |
| urchan_shapekey | 1.544 × 1.175 × 1.554 | **0.915 × 0.865 × 0.85** | **slot order changed `BASE, accent, windsheild, N` → `accent, BASE, windsheild, N`** | 3976/861/84/585 → 484/2690/80/505 | EMPTY → 18.2/4.6/6.9/8.6 % | 11 → 9, renamed `Armature|urchan_pitchdown` → `Armature|pitchdown` (file not drawn; but its 8 default clip ids are referenced by all four controllers — those ids are take-name derived and the takes were renamed) |

The checklist entry (`UNITY_VERIFICATION_CHECKLIST.md:68-100`) states "every bone's rest pose reproduced to 0.000" and "the
same file frame per file" — consistent with the probe (node TRS unchanged) — but says nothing about hull size; the sizes
above disagree with the artist files by 25-41 % on Dolphin / Manta / Urchan and by a 1.54 m X offset on the Sparrow, which
the Sparrow's root collider (`center (0,0,-3.5)`), the jet mounts (`b_Tail*` bones) and every fusion bake were fitted against.

---

## Rules (one line each, with the measurement)

1. Overwrite in place: same path, same guid, same node names/types, same Geometry names, same take names, same slot order —
   that is the only binding the asset record shows surviving (Squirrel splice; Sparrow Missile; `fileIdsGeneration 2` +
   `internalIDToNameTable []` on all 15 metas; the 4-decimal Rhino collider fit).
2. Never rename a node, bone, mesh node or take; never change a node's type; a renamed node is a missing object → "unused
   override", null field, feature silently off (CLAUDE.md § Shared prefabs; `VesselAnimation.cs:529-548` guards).
3. Keep the stripped-stub set bindable: root `//RootNode` trio, the skinned-mesh node, the armature, every bone a jet /
   collider / constraint / `m_RootBone` hangs on (Dolphin 18, Serpent 28, Squirrel 6, Sparrow 2 Transform stubs).
4. Reproduce the file frame exactly: `GlobalSettings` triple + `UnitScaleFactor` + root-node TRS + raw vertex frame
   (Squirrel `100, Z+,Y+,X−`, armature `R(71.89,−180,0)`; Sparrow same with mesh `R(0,0,180)`, armature `S 0.2034`; Serpent
   `100, Z+,Y−,X+` identity, prefab flips 180° Y ×2; Dolphin/Manta/Rhino/Urchin `1, Y+,Z+,X+`, roots `R(−90,0,0) S 100`
   or cm-baked).
5. Keep the hull's Unity-unit size, centre and nose axis within the artist's envelope (table (d)); the colliders, jets, camera
   offsets and fusion bakes were fitted to them — the WT rebuilds currently miss by 25-41 % on three hulls.
6. Four REAL element shapes (≥ 0.1 % of the root-bone bounds diagonal; shipped 2.5-17.9 %) named so that the last `.`-segment
   is `charge|mass|space|time` (any case), each with normals, weight 0 at rest, and **zero blend-shape animation curves**.
7. Keep every take's exact name; the meta's `clipAnimations` entries bind `takeName → internalID` and the controllers bind
   `(guid, internalID)` (Squirrel 9/9, Serpent 4/6, Sparrow 10/11 + 2 from SparrowModel4, Manta 8/9 all resolved); a new take
   needs a new entry, a dropped take leaves an empty clip.
8. Keep `optimizeGameObjects 0`, `isReadable 1`, `importBlendShapes 1`, `blendShapeNormalImportMode 1`, `meshCompression 0`,
   `bakeAxisConversion 0`, `useFileScale 1` + the file's `globalScale`; keep the root-motion bone names the metas pin.
9. Material names `Body/Domain/Window(+Engine)` or `BASE/accent/windsheild/N` in the shipped order with polygons on every slot;
   SMR slot 0 is the domain paint target and slot 3 the engine flare target (`VesselHelper.cs:166-168`,
   `VesselAnimation.cs:527-548`).
10. No polygon with a repeated/zero-length corner (Unity discards it, §4.7); after writing, verify with three readers
    (`fbx_probe`, `assimp`, a Blender re-import) AND compare Blender-world AABB / centre / window centroid to HEAD.

## Open questions (not measurable in this checkout)

- Unity's exact behaviour for a `clipAnimations` entry whose `takeName` is absent (empty clip vs. dropped entry) and for an
  unlisted take when the list is non-empty — stated from the in-repo evidence (lists shorter than the take set) plus Unity
  behaviour, not a test.
- The fresh-meta default for `avatarSetup` on a Generic import in this Unity version.
- Which node `-5228419308709608677` (Squirrel, `m_Layer 8` + second BoxCollider) is — the armature or a bone (ids not
  derivable offline).
- The Serpent's constraint scripts `1b37cff…` / `fff0960e…` are inferred (field names, manifest) to be Animation Rigging's
  `DampedTransform` / `RigBuilder`; no `Library/PackageCache` here to confirm.
- Whether the Manta's rotated prefab material array and never-painted domain (`Manta.md:28,279`) are intended.
- The exact Unity fileID hash: zero hits over 190 identifier forms × 4 hashes — treat as unknowable offline, as the
  asset-surgery skill already records.
