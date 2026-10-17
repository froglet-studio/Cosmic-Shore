# Manta — MODEL DOSSIER (measured 2026-10-17, branch ccr-d93a6dfb-ugbxng = origin/bleeding-edge on every file named here; `git diff --stat origin/bleeding-edge` on the prefab, both FBX, the driver and the controller is EMPTY)

> **Record of the ARTIST file as shipped before 2026-10-17.** On 2026-10-17 the file this dossier measures was
> rebuilt in place by `python3 Tools/Build/vessel_model/build.py manta` (spec `Tools/Build/vessel_model/specs/manta.py`,
> skill `/vessel-model`): same path and guid, same node, geometry, bone and material names, every bone's rest pose
> reproduced to 0.000 (`rest_compare.py`). Sections 1-7 describe the artist file the spec was written against and
> stay the explanation of WHY the spec keeps each name and number; section 8 is the contract the rebuilt file meets.
> Paths under `<scratchpad>/` were the measuring session's temporary files: the commands beside them regenerate
> every number from the repo.


Written for the modeller who will REPLACE the Manta's model with a better one on the same prefab, same gameplay, same code.
Every number was measured from the asset or the code; where a doc disagrees with an asset, the asset is reported and the doc is named.
Scratch evidence: `<scratchpad>/` — `probe/Manta_shapekey_rigged.txt`, `probe/mantis_shapekey_with_animations.txt` (fbx_probe --takes), `partmap/*.txt`, `manta_prefab_tree.txt` (annotated prefab hierarchy, `tools/prefab_tree.py`), `manta/geom_audit.txt` + `manta/geom_audit.py` (Blender audit in vessel space), `manta/bone_frames.py`, `manta/bind_vs_live.py` (prefab pose vs FBX bind pose), `manta/take_poses.txt` + `manta/take_poses.py` (every take sampled at its last frame), `manta/fbx_mats_curves.py` (material connection order + blend-shape curves via `Tools/Build/fbx_binary.py`), `ref_sheets/manta_{chase,front34,side,top,morph_*}.png`, `renders/Manta_shapekey_rigged_cy.png`.

Coordinate convention: **vessel space** = Unity units in the `Manta` root's frame, +Z nose, +Y up, +X starboard. FBX world → vessel space is `(−x, y, z) / 100` (same rule the Dolphin dossier re-proved from `Assets/_Scripts/Editor/VesselRigSwapper.cs:100` and `Tools/Build/verify_vessel_rig_puppetry_frames.py:550`); Blender re-import (Z-up) → vessel is `(−X, Z, −Y)`. Re-proved here: the prefab's native `Armature.002` carries exactly the FBX's `R(−90,0,0) S(100)` (prefab tree line 316) and the bones' head positions computed from the prefab's own native TRS (`bone_frames.py`) reproduce the Blender re-import's bone heads (`partmap/Manta_shapekey_rigged.txt`) under that mapping to 3 decimals (e.g. `Winghold.L` tail Blender `(0.998, 0.257, 0)` → vessel `(−0.998, 0, −0.257)` = computed bind head of `Wing.L`).

---

## 0. The verdicts the task asked for, in one screen

| question | answer (measured) | where |
|---|---|---|
| which hull is drawn | **`Manta_shapekey_rigged.fbx`** (guid `77a38b23d33b195469fe407e2207bbdf`), through a native `SkinnedMeshRenderer` on GO `manta` (`&7720489361082621337`) under the ACTIVE native GO `Manta_shapekey_rigged` | prefab lines 548–563 (`m_IsActive: 1`), 1519–1600 |
| the second hull | `mantis_shapekey_with_animations.fbx` (guid `7c324ef5b6621f84b894d9474c05ec3b`) is a full native copy too, but its root GO `mantis_shapekey_with_animations` is **`m_IsActive: 0`** (prefab line 1397+16) → never drawn, never animated. `Docs/VESSEL_CONSTRUCTION.md` §3.4 says "both `m_IsActive: 1`" — true of the two child GOs named `manta`, not of the parent that switches the second one off; the asset is the record | prefab line 1413 |
| how the FBX is referenced | **NOT a nested PrefabInstance.** Both hierarchies are UNPACKED into native GameObjects/Transforms; the FBX guids occur exactly 4 times in the prefab: two `m_Mesh` (lines 768, 1570) and two `m_Avatar` (lines 591, 1440). Zero `stripped` objects point into either FBX (the 12 stripped objects in the prefab belong to Skimmer/PipCamera/HUD/Tail/Notification instances) | `grep -n 77a38b23\|7c324ef5` |
| live animation driver | **`MantaAnimation` code puppetry** (prefab `&6293426128044408357`, part fields wired by reference to native bone Transforms). The Animator on the live hull (`&5658933110804061391`) has **`m_Controller: {fileID: 0}`** and an avatar whose import FAILED (`rigImportErrors: "Avatar creation failed: Cannot find root motion transform 'Chassis.002'"`, rigged `.meta` line 19) → it plays nothing. `MantaAnimatorController` is only on the INACTIVE mantis copy's Animator (line 1440–1441). `YawsteryActionExecutor.shipAnimator` points at the controller-less live Animator and its `TryTriggerAnimator`/`TrySetAnimatorFloat` (lines 246–256) have NO callers and the action SOs' `animatorParam*` strings are empty | prefab 582–603, 1431–1452; `YawsteryAction-Left/Right.asset` lines 32–34 |
| domain colour | **never painted on this hull**: `VesselCustomization._shipGeometries` lists 7 BONE GameObjects (`Wing.R, Wing.L, WingT.R, WingT.L, WingB.R, WingB.L, chassis.003`), none carries a Renderer; `_domainReplacesMaterials` is unset → `ShipHelper.ApplyShipMaterial` iterates them and writes nothing. The hull wears the prefab's fixed material array (§1.3) | prefab lines 1704–1711 of the tree; `VesselCustomization.cs:88–96`; `VesselHelper.cs:156–181` |

---

## 1. LIVE MODEL

| fact | value | measured at |
|---|---|---|
| prefab | `Assets/_Prefabs/Spacevessels/Manta.prefab`, 4,550 lines | `wc -l` |
| live FBX | `Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx`, 301,340 B, FBX 7400 | `ls -la`, probe line 1 |
| FBX guid | `77a38b23d33b195469fe407e2207bbdf` | `.meta` line 2 |
| second FBX (inactive copy) | `Assets/_Models/Vessel Models/mantis_shapekey_with_animations.fbx`, 1,138,828 B, guid `7c324ef5b6621f84b894d9474c05ec3b` | `.meta` line 2 |
| host chain | root `Manta` (GO `6417075533431866457`, identity) → `OrientationHandle` (GO `1878660672777590133`, identity; `m_Children` order: RearViewCamera stub, ThiefSkimmer stub, `Manta_shapekey_rigged`, `mantis_shapekey_with_animations`) → `Manta_shapekey_rigged` (GO `1379066679125974031`, **identity TRS**, Animator) → `Armature.002` (GO `3427005755627130874`, `R (−0.7071, 0, 0, 0.7071)` = −90° about X, **scale 100**) + `manta` (GO `5691083765198909859`, identity, the SkinnedMeshRenderer) | tree lines 306–316, 377–385 |
| instance transform | no FBX instance exists; the native root `Manta_shapekey_rigged` is at `pos (0,0,0) rot (0,0,0,1) scale 1`; the armature's 100× and −90° are the FBX's own | tree lines 312–316 |
| SkinnedMeshRenderer | `m_Mesh {fileID: 322997229772504633, guid: 77a38b23…}`, `m_RootBone` = `chassis` (`4769814507153169321`), 15 `m_Bones`, `m_BlendShapeWeights [0,0,0,0]`, `m_UpdateWhenOffscreen 0`, `m_AABB center (0.0005, −0.0049, −0.0012) extent (0.0286, 0.0169, 0.0045)` in root-bone space (×100 → 5.7 × 3.4 × 0.9 u, wider than the bind hull — a stale editor bound, not geometry) | prefab 1569–1595 |

### 1.1 Units, scale, axes (measured, not assumed)

- Rigged FBX `GlobalSettings`: **`UnitScaleFactor = 1.0`** (file units are centimetres — NOT 100), `up = +Y`, `front = +Z` (probe line 3 `up=+1 front=+2 coord=+0`). The mesh node `manta` is at **identity** with vertices baked in cm: mesh-local bounds `(−197.6, −35.74, −185.7)..(197.6, 22.79, 41.77)`, diag 459.674 (probe line 25). The armature `Armature.002` carries `R(−90,0,0) S(100)` (probe line 6).
- mantis FBX: same `UnitScaleFactor 1.0`, but the mesh node carries `R(−90,0,0) S(100)` and vertices in metres (bounds `±1.976`), so both land at the SAME world size; `VESSEL_CONSTRUCTION.md` §3.4's "same export at 100× different unit scale" is confirmed by the material index counts and vertex counts being identical (`fbx_mats_curves.py`).
- `.meta` (rigged): `globalScale 1`, `useFileScale 1`, `useFileUnits 1`, `bakeAxisConversion 0`, `optimizeGameObjects 0`, `isReadable 1`, `importBlendShapes 1`, `blendShapeNormalImportMode 1`, `maxBonesPerVertex 4`, `minBoneWeight 0.001`, `optimizeBones 1`, `sortHierarchyByName 1`, `fileIdsGeneration 2`, `animationType 2` (Generic), `clipAnimations []`, `materials.externalObjects {}` (no remaps — irrelevant, the prefab's native SMR array overrides). mantis `.meta`: `maxBonesPerVertex 26`, `minBoneWeight 0.5`, four material remaps, nine clipAnimations (§3.4).
- Net Unity size = cm × 0.01 = **1 : 1 with the Blender metres**. Every bone GameObject sits under the 100× armature, so bone-hosted colliders are authored at 1/100 (§6).
- **Hull bounds at BIND, vessel space (Unity units)**: x `[−1.9756, +1.9756]`, y `[−0.3574, +0.2279]`, z `[−1.8573, +0.4177]` → **3.951 wide × 0.585 tall × 2.275 long**, diagonal 4.597 (`geom_audit.txt` line 1; `measure_vessel_models.py --only anta`: `extent (395.118, 58.522, 227.503)` cm). The origin sits 0.418 u behind the nose = 18 % of the length; 82 % of the hull is BEHIND the origin.
- **Hull bounds as DRAWN** (the prefab's native bone pose differs from the FBX bind pose, §3.2): wing tips `(∓2.315, 0, −0.810)` (bind `(∓1.976, 0, −1.5)`), fin tips y `−0.589 .. +0.610` (bind `−0.357 .. +0.228`), nose/stern unchanged → ≈ **4.63 wide × 1.20 tall × 2.28 long** (`bind_vs_live.py`).
- **Nose axis = FBX world +Z = Unity +Z.** Evidence: the canopy polygons (slot 2 `windsheild`, 38 polys) sit on the chassis top at z `[−0.280, +0.383]`, the +z end (`geom_audit` SLOT 2 bbox); the chassis tapers to a 0.708 × 0.294 cross-section at z +0.418 (partmap "cross-section near −Y end"); the four fins (`WingT/WingB`) and the three stern spikes lie at z `[−1.857, −0.877]`; the wing tips trail at z −1.5 (an arrowhead sweep); the mantis take `pitch up` raises the `fusal` bone +24.2° toward **+y** and `yaw right` turns it +22.1° toward **+x** (`take_poses.txt`); the camera follows at `(0, 0, −30)` and the tail hangs at `z −31.5`. In the mesh's OWN (Blender, Z-up) coordinates the nose is **−Y** (mesh y = −0.418); the −90° X rotation makes it FBX +Z and Unity keeps Z while mirroring X — so the artist's `.L` bones land at Unity **−x = port**.

### 1.2 Node hierarchy (FBX exact names; prefab native names where they differ; `skins N` = vertices weighted to that bone; head = bone head in vessel space, bind pose)

```
manta [Mesh]  T0 R0 S1  geometry "Sphere.018"  materials [accent, BASE, windsheild, N]   ← Unity Mesh is named "manta" (the Model node; asset-surgery SKILL.md §4.8c)
Armature.002 [Null]  R(−90,0,0) S(100)                                                     ← Unity GO "Armature.002", localScale 100
  chassis        head (0, 0, −0.469) → +z, len 1.00   skins 453   fuselage (x±0.418, y[−0.143,+0.176], z[−1.461,+0.418])   [mantis file: "root" 0 verts + "fusal" 453]
    chassis.001  head (0, 0, +0.531) → −z, len 0.649  skins 0
      Winghold.L   head (0,0,−0.118) FBX R(0,0,−82.05)   skins 0      [prefab: Winghold.L, re-posed to +100°, §3.2]
        Wing.L     head (−0.998, 0, −0.257)            skins 530    main wing, cluster x[−1.976,−0.692] y±0.044 z[−1.715,+0.093]
      chassis.003  head (0,0,−0.118) → −z, len 0.645  skins 0      hosts the fuselage BoxCollider (§6)
        WingThold.L  head (0,0,−0.763) R(8.10,5.10,−64.39)  skins 0  [prefab name: WingBottomhold.Left]
          WingT.L    head (−0.653,+0.102,−1.066)       skins 456    upper-left fin (bind), cluster x[−1.184,−0.502] y[+0.054,+0.228] z[−1.857,−0.877]
        WingBhold.L  head (0,0,−0.763) R(−12.27,−7.61,−63.46) skins 0 [prefab name: WingTophold.Left]
          WingB.L    head (−0.653,−0.155,−1.066)       skins 456    lower-left fin (bind), y[−0.357,−0.103]
        WingThold.R / WingT.R, WingBhold.R / WingB.R   x-mirrored, same counts          [prefab: WingBottomhold.Right / WingTophold.Right]
      Winghold.R → Wing.R  head (+0.998, 0, −0.257)   skins 544
```
(probe lines 5–21; clusters `geom_audit.txt` PART rows.) 15 bones, 17 model nodes, 15 skin clusters, 7 skinning bones. The mantis file has 16 bones: `root` (0 verts) + `fusal` (453) replace `chassis`; everything else is identical.

### 1.3 Polycount, materials, UVs

- **3,156 polygons** = 2,891 quads + 44 triangles + **221 n-gons** (113×5, 44×6, 12×7, 48×8, 3×16, 1×17) = **6,746 triangles** after fan triangulation; **3,351 source vertices**; Unity imports **7,119** (split by normals/UVs: `Assets/_SO_Assets/CrystalHullFusion/Manta_*_HullFusionBake.asset` `hullVertexCount: 7119`). (`geom_audit.txt` POLYGONS line; probe line 25.)
- One mesh, **7 closed shells** (sizes 544, 530, 456 ×4, 453 = one per part, NOT welded to each other), **0 boundary edges, 0 non-manifold edges, 0 zero-area polygons** — the cleanest topology in the measured fleet (the Dolphin has 36 open boundaries).
- **UVs**: one layer `UVMap`, range u `[0.000, 0.969]`, v `[0.000, 0.943]` — packed inside 0..1 (texturable, unlike the Dolphin). No vertex colours.
- **Material slots** — FBX connection order (`fbx_mats_curves.py`: `accent → BASE → windsheild → N` in both files) = Unity submesh order; polygon counts `ByPolygon {0: 174, 1: 2057, 2: 38, 3: 887}`; areas in vessel u² (total 10.29 u²):

| slot | FBX material | FBX authoring | polys | area | where on the hull | **material the prefab puts on it** | role as consumed |
|---|---|---|---|---|---|---|---|
| 0 | `accent` | black, **emissive (1, 0.395, 0)** orange | 174 | 0.689 (6.7 %) | the oval "eyes" and dots on wings (28/29), fins (21 each) and the chassis spine (33) | `Assets/_Graphics/Materials/BlueBaseVesselMaterial.mat` (`_Color1 (0, 0, 0.16)` dark navy) | **`FlareBody()` → `materials[0]`** (lines 537–548 of VesselAnimation) — nothing calls it on this hull. Would be the DOMAIN slot by the Squirrel/Dolphin convention; here it is dark navy |
| 1 | `BASE` | black | 2,057 | 8.846 (86 %) | every hull surface | `ScreenVesselMaterial.mat` (`_Color1 (0.47, 0.47, 0.71)` lavender-grey, `_Color2 (0, 0, 0.5)`) — the fleet's WINDOW material | BODY. (`VESSEL_CONSTRUCTION.md` line 342 "Manta family (serialized) Body, Window, Domain, ?" describes the prefab array in the order the author INTENDED; measured against the FBX's actual submesh order it lands Window-material on the body) |
| 2 | `windsheild` | diffuse (0.056, 0.058, 0.8) blue | 38 | 0.404 (3.9 %) | the canopy dome on the chassis top, x ±0.383, y [0.057, 0.161], z [−0.280, +0.383] | `GreenAccentVesselMaterial.mat` (`_Color1 (0, 0.094, 0.188)`, `_Color2 (0, 0.776, 1.498)` HDR cyan) | the canopy glows accent-cyan |
| 3 | `N` | black, **emissive (0, 0.211, 1)** blue | 887 | 0.353 (3.4 %) | thin trailing-edge/rib stripes on wings (153–156) and fins (144 each), z [−1.448, −0.112] | `LimeVesselMaterial.mat` (`_Color1 (0, 0.80, 0.007)` green) | **`FlareEngine()` → `materials[3].SetFloat("_ColorMultiplier", 5)`**, edge-triggered by `VesselTransformer.cs:1466–1472` when `throttleMultiplier > 1` (Soar). The stripes ARE the engine glow |

All four prefab materials use `VesselGraph.shadergraph` (guid `0236447d…`; properties `Color1`, `Color2`, `ColorMultiplier`), so both flares have a property to land on. The mantis `.meta` remaps (BASE→Blue, N→Lime, accent→GreenAccent, windsheild→Screen) show the artist's intent — body navy, stripes lime, eyes accent-cyan, canopy screen — but the live SMR's native `m_Materials` array `[Blue, Screen, GreenAccent, Lime]` is what draws, and it is rotated one slot against the FBX order. Unverified in-editor; measured from the asset.

The card art (`Assets/_Graphics/CardImages/Manta_large.png`, 512 px; `Manta.png` 256 px) is a front-¾ render of this same model in its **FBX authoring colours**: black hull, orange-glowing eye ovals, blue canopy.

---

## 2. WHAT THE PREFAB REFERENCES INSIDE THE FBX

Because both hierarchies are unpacked, the prefab references only TWO sub-assets of the live file, plus the bones by the prefab's OWN native fileIDs:

| reference | fileID / value | what binds to it |
|---|---|---|
| **Mesh** | `322997229772504633` (guid `77a38b23…`), line 1570 | the live SMR. `fileIdsGeneration: 2` hashes TYPE + NAME; the name is the **Model node `manta`** (Unity names the Mesh after the Model node, not the Geometry `Sphere.018`: asset-surgery SKILL.md §4.8c "the mesh name in the message is the Model node"). Both FBX files produce the identical id because both Model nodes are `manta`. **Keep the Model node named `manta`** (and, to be safe, the geometry `Sphere.018`) |
| **Avatar** | `9000000` (guid `77a38b23…`), line 591 | the controller-less Animator. The rigged avatar failed to build (`.meta` line 19) and nothing plays through it; harmless, but `rootMotionBoneName: chassis` in the `.meta` (line 101) and the stale `motionNodeName`-style error name `Chassis.002` mean a replacement whose root bone is not `chassis` only changes an error nobody reads |
| **15 `m_Bones`** (native fileIDs, prefab 1571–1586) | `chassis, chassis.001, Winghold.L, Wing.L, chassis.003, WingBottomhold.Left(=WingThold.L), WingT.L, WingTophold.Left(=WingBhold.L), WingB.L, WingBottomhold.Right(=WingThold.R), WingT.R, WingTophold.Right(=WingBhold.R), WingB.R, Winghold.R, Wing.R` | this order IS the FBX deformer/cluster order (probe "bones (15)" list, same order). The Mesh's bindposes are indexed by it. **A replacement must export exactly 15 clusters in this order** — the prefab's `m_Bones` are not rewritten by an FBX re-import |
| **Bone rest TRS** (native, prefab tree 318–376) | the live values — NOT the FBX's for the hold bones (§3.2) | the Mesh's bindposes come from the NEW file, the bone transforms from the OLD prefab: the drawn pose = new bind composed with the prefab's native pose. A replacement that keeps the FBX's bone rest TRS keeps today's 18° wing-forward / fin-swap look; one that changes a bone's rest TRS changes the drawn pose by exactly that delta |
| `MantaAnimation` part fields (prefab `&6293426128044408357`) | `Fusilage → chassis (4769814507153169321)`, `LeftWing → Wing.L (8786154189335303190)`, `RightWing → Wing.R (5571582515015820382)`, `ThrusterTopRight → WingTophold.Right (8105609640698425209)`, `ThrusterTopLeft → WingTophold.Left (7992807412611599177)`, `ThrusterBottomLeft → WingBottomhold.Left (6568354364218703309)`, `ThrusterBottomRight → WingBottomhold.Right (8043265874885454852)`, `SkinnedMeshRenderer → &7720489361082621337` | by native reference; `MantaAnimation` has NO `ResolvePart` candidates (it never overrides `ResolveParts`), so names are irrelevant to it |
| `VesselCustomization._shipGeometries` | `Wing.R, Wing.L, WingT.R, WingT.L, WingB.R, WingB.L, chassis.003` (native GOs) | legacy per-part list; none has a Renderer (§0) |
| four `Manta_{Charge,Mass,Space,Time}_HullFusionBake.asset` | `hullMesh {fileID: 322997229772504633, guid: 7c324ef5…}` — the **mantis** file's mesh, `hullVertexCount 7119`, `hullHash 4031086348` (bake asset lines 16–19) | baked 2026-10-08 (commit `376b525b1`, Garrett). At runtime `CrystalHullFusion.FindHull(root, bakedKey: bake.HullMesh)` (CrystalHullFusion.cs:596, 744) accepts the keyed mesh only on a `Shown` renderer (`activeInHierarchy`, line 255) — the mantis renderer is inactive — so it falls through to `FindHullRenderer` → the rigged SMR, and `Matches()` line 84 rejects: *"it was solved for hull 'manta', the vessel draws 'manta'"* (two different assets with one name). As the assets show, the Manta's fusion bakes are already mismatched by reference; a replacement must re-bake against the LIVE mesh in the editor (FrogletTools, CrystalHullFusion) |
| sibling prefabs | `Falcon.prefab`, `Shrike.prefab`, `Termite.prefab` each hold the SAME two meshes (1 ref each to `322997229772504633` under both guids), the same `MantaAnimation` script guid `46ca128b16ccbfd42be69beb8b52be77` and the same controller guid | an in-place overwrite of the FBX re-skins all four vessels (`Docs/claude/ABOUT_PROJECT.md:23`: Falcon is a non-spawnable Manta copy) |

**Exact node names a replacement must keep** (same file, same name ⇒ same fileID / same cluster order): Model node **`manta`** (the Mesh id), geometry `Sphere.018` (safe), armature `Armature.002`, and the 15 bones in this hierarchy and cluster order: `chassis, chassis.001, Winghold.L, Wing.L, chassis.003, WingThold.L, WingT.L, WingBhold.L, WingB.L, WingThold.R, WingT.R, WingBhold.R, WingB.R, Winghold.R, Wing.R`. Shape names `Mass, charge, space, time`. Material names `accent, BASE, windsheild, N` in that connection order.

---

## 3. ANIMATION

### 3.1 Live driver — `MantaAnimation : VesselAnimation` (code puppetry, 84 lines)

`Assets/_Scripts/Controller/Animation/MantaAnimation.cs`, on the `Manta` root (prefab `&6293426128044408357`). Serialized on the prefab (tree lines 183–197): `SaveNewPositions 1`, `brakeThreshold 0.65` (unused by this subclass), `lerpAmount 2`, `smallLerpAmount 0.7`, **`smallAnimationScaler 27`, `mediumAnimationScaler 30`, `bigAnimationScaler 60`** (the C# defaults 10/30/45 are overridden by the prefab).

Inputs: `VesselAnimation.Update` (lines 27–35) calls `PerformShipPuppetry(pitch = YSum, yaw = XSum, roll = YDiff, throttle = XDiff)` (`DualStickMix.cs:7`: "XSum = yaw, YSum = pitch, XDiff = speed, YDiff = roll"); single-stick mode passes `(EasedLeftJoystickPosition.y, .x, 0, 0)`. **Throttle is 0..1, cruise 0.5** (`TouchInputStrategy.cs:87 heldXDiff = 0.5`, keyboard W/S), so the throttle terms below carry a permanent offset in flight. `InputStatus.Idle` → `Idle()` lerps every part back to its `InitialRotations` at 0.7/s (`SaveNewPositions 1`, lines 322–347).

`RotatePart(part, pitch, yaw, roll, initial)` (VesselAnimation.cs:384–393): `localRotation ← Lerp(current, Quaternion.Euler(pitch, ROLL, YAW) · initial, 2·dt)` — note the argument order: the method's `yaw` lands on Euler **Z** and `roll` on Euler **Y**, in the part's PARENT frame, premultiplied onto the rest captured at `AssignTransforms` (lines 66–83: `InitialRotations[0..6]` = the prefab's native `localRotation` of chassis, Wing.L, Wing.R, WingTophold.Left, WingTophold.Right, WingBottomhold.Left, WingBottomhold.Right). No `CaptureRestRotations`/`RotatePartFromRest` (this class predates them); no drift handling; no positions move. Lerp 2/s ⇒ ~0.5 s time constant.

Parent-frame axes in vessel space (`bone_frames.py`), and therefore what each Euler term physically does:

| part (prefab name = FBX bone) | parent frame X / Y / Z in vessel space | call (MantaAnimation.cs lines) | motion |
|---|---|---|---|
| `chassis` (Fusilage) | X = +x, Y = −z, Z = +y | `Euler(pitch·30, −roll·30, yaw·30)` (22–26) | whole hull (and every child) pitches about +x by 30°/unit, rolls about the long axis 30°/unit, yaws about +y 30°/unit |
| `Wing.L` (LeftWing; parent `Winghold.L`) | X = (−0.17, 0, −0.98) ≈ −z, Y = (−0.98, 0, +0.17) ≈ the wing's span axis, Z = +y | `Euler(pitch·30, (−roll−pitch)·27, (−throttle+yaw)·60)` (28–32) | **pitch FLAPS the wing** (dihedral, ±30°), **roll+pitch TWISTS it about its span** (±27°), **throttle and yaw SWEEP it in the plane** (60°/unit: −30° at cruise, −60° at full throttle, ±60° of yaw) |
| `Wing.R` (parent `Winghold.R`) | X ≈ +z, Y ≈ +x span, Z = +y | `Euler(pitch·30, (−roll+pitch)·27, (throttle+yaw)·60)` (34–38) | mirror; the throttle sign is mirrored so both wings sweep the same way with speed |
| the four fin HOLDS (ThrusterTopLeft/TopRight/BottomLeft/BottomRight; parent `chassis.003`) | X = +x, Y = −z, Z = +y | `Euler(pitch·30, (−roll ± throttle)·27, −yaw·30)` (40–62): `+throttle` on BottomLeft and TopRight, `−throttle` on TopLeft and BottomRight | fins pitch with the hull (30°/unit), counter-yaw (−30°/unit), and swing about the long axis: roll swings all four the same way (−27°/unit), throttle SCISSORS them (one diagonal pair opens, the other closes, 13.5° at cruise, 27° at full) |

Sign convention: Unity's left-handed Euler — `+pitch` about +x drops the nose (+z → −y), `+yaw` about +y turns the nose to +x (starboard), the `−roll` on the chassis means a positive `YDiff` rolls the hull about **+z**. `mirrorAppendageRoll`-style flags do not exist here; nothing compensates sign, so a replacement's REST must be the ship's own frame (nose +z, up +y) and its bone parents must keep these frame orientations, or every term lands on a different physical axis.

**`CollectElementShapes` includes inactive renderers** (`GetComponentsInChildren<SkinnedMeshRenderer>(true)`, VesselAnimation.cs:501) → 8 shape targets are driven on this prefab (4 on the live SMR + 4 on the inactive mantis SMR), the morph audit sees 8 (VESSEL_CONSTRUCTION §3.4). Harmless, but a replacement that removes the dead copy halves it.

### 3.2 Rest pose — the prefab's native bones are NOT the FBX bind pose (measured, `bind_vs_live.py`)

The mantis copy (lines 1397–1518) is a pristine import of the shared bind pose; the LIVE copy's hold bones were re-posed in the prefab:

| bone (FBX name) | prefab name | bind +Y direction (vessel) | LIVE +Y direction | swing | elevation bind → live |
|---|---|---|---|---|---|
| `Winghold.L` | same | (−0.99, 0, −0.14) 8° aft of lateral | (−0.98, 0, +0.17) 10° FORWARD of lateral | **18.0°** (local z 82.05° → 100°, tree line 358) | 0 → 0 |
| `Wing.L` | same | (−0.54, 0, −0.84) | (−0.78, 0, −0.62) | 18.6° (carried by the hold; own local 49.05° → 48.44°) | — |
| `WingThold.L` | **WingBottomhold.Left** | (−0.90, +0.14, −0.42) | (−0.78, −0.47, −0.42) | 36.4° | **+8.1° → −28.2°** |
| `WingBhold.L` | **WingTophold.Left** | (−0.89, −0.21, −0.41) | (−0.75, +0.52, −0.41) | 43.3° | **−12.2° → +31.0°** |
| right side | mirrored | | | same | same |

Consequences for the drawn hull (rigid-cluster transport `M_live · M_bind⁻¹ · p`): wing tips `(∓1.976, 0, −1.50)` → **`(∓2.315, 0, −0.81)`** (span 3.95 → 4.63 u, tips 0.69 u further forward); the FBX's TOP fins (`WingT`) are drawn BELOW the body (tip y +0.228 → **−0.589**) and the BOTTOM fins (`WingB`) ABOVE (−0.357 → **+0.610**): the prefab's "Top/Bottom" hold names describe where the fins END UP, inverted against the FBX's T/B. The modelled flat-ish X-tail (+9°/−13°) is drawn as a ±31–40° X. Nose and stern are unmoved. A modeller judging the shipped look from the FBX alone sees a different ship than the player does: apply `Winghold.* z ±100°` and the two hold quaternions `(±0.0991, ±0.3402, ±0.5334, 0.7681)` / `(∓0.1049, ∓0.2889, ±0.5298, 0.7905)` (tree lines 334–352) to see the in-game rest.

### 3.3 The Animator path (present, inert) — `Assets/_Models/Animations/MantaAnimatorController.controller` (guid `f922b4ddeaa211f498ff3a539e6ce230`)

Parameters: `Blend, Pitch, Yaw, Roll, Throttle` (all float, default 0). One layer, one state `Blend Tree` (default), `m_TimeParameterActive 1` with `m_TimeParameter: Blend`, motion = BlendTree `−3065510467884305016` (**type 4 = Direct**, both children weighted by `Blend`; thresholds 0.25 / 1):

| tree | type | params (x / y) | children: clip (internalID) @ position |
|---|---|---|---|
| `1054661598827968095` | 3 = 2D Freeform Cartesian | `Yaw` / `Pitch` | `pitch down` (`6212661447819486376`) @ (0, +1); `pitch up` (`3804817242277331963`) @ (0, −1); `yaw right` (`−6822812431335815528`) @ (+1, 0); `yaw left` (`−6953860090002917003`) @ (−1, 0) |
| `−5535465862024685328` | 3 = 2D Freeform Cartesian | `Roll` / `Throttle` | `speedup` (`−7942830232961210933`) @ (0, +1); `speeddown` (`1932594824745245842`) @ (0, 0); `roll left` (`−8679543024212152096`) @ (+1, 0); `roll right` (`−721875957892904936`) @ (−1, 0) |

Orphan sub-assets (unreachable from the state machine): `−6502134309559327549` (same four clips on params `z`/`w`, roll positions inverted, speedup at (0, 2)); `urchan fly`, `throtle`, `roll` trees pointing at the **Urchin** FBX (guid `7f9bbc7b…`, params `pitch/yaw/speed/roll` that are not declared); five empty trees. `cut` (`2905659382556172114`) is bound by nothing.

The mantis `.meta` `clipAnimations` (lines 72–301): nine clips, each `name = takeName = "Armature.002|<take>"`, **`firstFrame 19, lastFrame 20`** — one frame sliced from the END of a 21-key, 0.833 s take ⇒ every Unity clip is a **POSE** (the take's full deflection), while the FBX takes themselves are MOTIONS (21 keys, 0 → deflection). `loop 0`, `keepOriginalPositionY 1`, `bodyMask` full.

Sampled at the last frame (`take_poses.txt`, vessel space; `fusal` = the mantis fuselage bone, bind direction +z):

| take | fuselage | wing tips (L / R displacement) | fins / stern | reads as |
|---|---|---|---|---|
| `pitch up` | **elevation +24.2°** (nose toward +y) | both tips DOWN 0.76 | bottom fin tip −0.67 y | **nose UP in Unity ✓** |
| `pitch down` | −23.3° | both tips UP 0.68 | top fin / stern +1.07..1.14 y | nose down ✓ |
| `yaw left` / `yaw right` | azimuth **−20.9° / +22.1°** (nose toward −x / +x) | tips swing 0.5–1.1 | | left = port ✓ |
| `roll left` | 0 | L tip −1.64 y, R tip +1.64 y | | left wing drops ✓ bank left |
| `roll right` | 0 | L +1.62, R −1.63 | | bank right ✓ |
| `speedup` | 0 | tips inboard 0.71 and aft 0.59 | fins fold aft 0.3 | wings fold back ✓ |
| `speeddown` | 0 | tips outboard 0.53 and **forward 1.25** | fins spread, +0.75 z | airbrake ✓ |
| `cut` | 0 | 0 | `WingThold.L` 6.8°, `WingT.L` 3.4° only | a stub (the sting?) |

Direction convention: the takes deflect the way their names say in Unity space. The controller's trees, however, place `pitch down` at `Pitch = +1` and `roll left` at `Roll = +1` — so a positive `Pitch` parameter would have played NOSE-DOWN and a positive `Roll` a LEFT bank. Nothing writes these parameters (§0), so this is documentation only; `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md:83` ("`MantaAnimationContoller` → Animator floats Pitch/Yaw/Roll/Throttle … keep binding") describes the Squirrel's use of its own controller, not this prefab.

**Blend-shape curves in the takes**: every take carries four `DeformPercent` curves (`Mass, charge, space, time`), 2 keys, **constant 0.0** (`fbx_mats_curves.py` — unlike the Dolphin's constant-100 residue). Harmless twice over: the Animator never plays them and `VesselAnimation.LateUpdate` is the single morph writer (CONTRACT §7). A replacement should export none.

### 3.4 Flare slots
`FlareEngine/StopFlareEngine` → `materials[3]` = `LimeVesselMaterial` on the FBX `N` stripes (887 polys, trailing edges of wings and fins) — driven by Soar (`throttleMultiplier > 1`, VesselTransformer.cs:1466). `FlareBody` → `materials[0]` = `BlueBaseVesselMaterial` on the `accent` eye-spots — no caller on this hull. A replacement must keep slot 3 = the surfaces that should glow on boost and slot 0 = the surfaces that would carry the domain/charge read.

---

## 4. ELEMENT MORPHS

Discovery: `CollectElementShapes` → `VesselElementalMorphConfigSO.TryResolveElement` (tail after the last `.`, case-insensitive match of the whole tail/token against `Charge/Mass/Space/Time`, lines 60–74) — the shipped `Mass` (capital) resolves. Weight = `NormalizedMorphWeight(level) × last-frame weight (100)`; glide `morphDuration 0.75`, `morphEase 4` (`Assets/Resources/VesselElementalMorphConfig.asset` lines 15–16); written in `LateUpdate`.

Shipped shapes — identical in both files (probe lines 27–30; vessel-space effects from `geom_audit.txt`, bind pose; diag 4.597):

| shape | verts moved | max travel | what it does (measured, mean delta per part) | reads as |
|---|---|---|---|---|
| `Mass` | 2,125 (63 %) | **0.553 u = 12.03 %** REAL | wings slide/stretch **FORWARD** (+0.25..0.26 z on `Wing.*`, +0.09 on `WingT.*`, +0.04 on `WingB.*`), chassis +0.008 y; hull z-max 0.418 → **0.646** (a wing leading edge passes the nose), y-max 0.228 → 0.293 | the ship lunges/extends forward — reach or sweep, NOT volume |
| `charge` | 2,244 (67 %) | 0.336 u = 7.32 % REAL | everything spreads OUTWARD and aft: wings ±0.074 x / −0.049 z, fins ±0.046..0.048 x / −0.045 z, chassis ≈0; bounds x ±1.976 → **±2.122**, z-min −1.857 → −1.984 | a general spread; at 30 u it is a 4 % change of silhouette — barely legible |
| `space` | 2,606 (78 %) | **0.820 u = 17.83 %** REAL | wings DROOP (−0.134..−0.136 y) and EXTEND: tips x ±1.976 → **±2.615**, y-min −0.357 → −0.561; fins −0.034..−0.040 y; chassis ≈0 | reach ✓ (span +0.64 u) plus an anhedral droop |
| `time` | 1,160 (35 %) | 0.718 u = 15.62 % REAL | everything SWEEPS AFT: wings −0.28 z, chassis −0.095, fins −0.07..−0.08; z-min −1.857 → **−2.433** | speed ✓ (a longer, swept hull) |

All four REAL (threshold `MinShapeTravelFraction`, measured fleet gap 2.46–17.94 % vs 0; CONTRACT §7). Shapes carry normals (probe `normals=True`). Note the two-hull duplication: the morph audit counts 8 (§3.1).

### 4.1 What each element should express on THIS hull (fleet rule: Space = reach/presence, Time = rate/mobility, Charge = threat/energy, Mass = size/volume), mapped to the shipped abilities (`element_ability_table.py Manta`: 4/4 abilities, 4/4 scaling, 3/4 L5; `MANTA_STING_KABLOOM.md` table; the map asset `Assets/Resources/ElementalAbilityMaps/Manta.asset` is the record)

| element | shipped ability (asset) | the thing that grows | proposed geometric change | target travel (of 4.60 diag) |
|---|---|---|---|---|
| **Charge** | **Sting**, passive: bomb-bay capacity 3 → 5 (`capacityPerChargeLevel 0.2`, `maxCapacity 5`) and skim-charge rate ×1 → ×2 (`chargeRateAtFullCharge 2`), `MantaStingConfig.asset`; L5 Contagion | the BAY: how many bombs the belly holds and how hot it runs | swell the chassis BELLY and stern (the bomb bay) radially ~0.12 u and raise the three stern spikes into a visible launcher/ovipositor (spikes +0.15 z aft, thicker); light the `accent` eye-spots (slot 0 is the `FlareBody` slot) — keep wings and fins still so Charge reads on the body alone | 0.25–0.40 u (5–9 %) |
| **Mass** | **Yastri**, Input 12 (`OnlyLeftStickAction`): trail prism VOLUME 1 → 2.5 (`VesselPrismController.trailVolume`, prefab), turn rate unscaled; L5 Shielded Turn Trails | the TRAIL's volume — the mass the hull lays down | thicken the hull: wing and fin cross-sections ×1.6 (0.088 → 0.14 u thick), chassis y ±0.16 → ±0.26, canopy taller; **no forward slide** (the shipped Mass moves the wings +0.26 z, which is Space's vocabulary) | 0.30–0.45 u (6–10 %) radial |
| **Space** | **Kabloom**, passive: bloom scale ×1 → ×1.6 (`blastScaleAtFullSpace`, `kabloomBlastScale 140`) and the skimmer's reach 20 → 40 (nested `Skimmer` scale, `ElementalFloat`); L5 No Friendly Fire | how far the blast and the skim REACH | keep the shipped idea and clean it: extend the wing tips (+0.64 x, as shipped) and the fin tips (+0.25 along each fin) WITHOUT the droop (the −0.2 y anhedral reads as "sag", not reach); add a 0.1 u splay of the stern spikes | 0.60–0.70 u (13–15 %) at the tips — the shipped 0.82 is at the ceiling of the fleet's real range |
| **Time** | **Soar**, Input 13 (both triggers): max speed ×1 → ×1.3 (`BoostSpeedMultiplier`, floor 0.7); L5 OPEN | speed | keep the shipped sweep (everything −0.28..−0.58 z) and sharpen it: chassis nose lengthens +0.15 z, canopy lowers 0.03, fins fold 10° toward the body — the `speedup` take's silhouette, baked | 0.55–0.70 u (12–15 %) |

Name the shapes exactly `charge`, `mass`, `space`, `time` (or `Key.mass` etc. — only the tail after the last `.` is read); last-frame weight 100; export normals (`blendShapeNormalImportMode 1`); export NO `DeformPercent` curves.

---

## 5. IDENTITY

- `VesselClassType.Manta = 1` (`Assets/_Scripts/Data/Enums/VesselClassType.cs:13`); "Feature-complete playable vessel" (`Docs/claude/ABOUT_PROJECT.md:15`); `VesselStatus._name Manta`, `vesselType 1`, `boostMultiplier 4` (tree 61–71). Falcon (9), Shrike (10), Termite (8) are flat copies of this prefab.
- **Role — "the bomb ray", "Destroyer → Nuke"** (`MANTA_STING_KABLOOM.md` lines 1–9, `FLEET_MAPS.md:203`): *buttonless where it matters* — skim prisms to ARM bombs (0.34 charge per skim, 1.5 s per-prism cooldown, bay 3–5), graze a rival or a creature to PLANT one (one bomb per target, 25 s fuse, blast 70), touch a crystal to KABLOOM every planted bomb (blast 140, ×1.6 at Space 10, cascade 0.09 s stagger). The two HELD inputs are movement: **Soar** (both triggers, analog boost, `MantaAnalogTurnBoostExecutor`, `maxYawDegPerSec 60`) and **Yastri** (one trigger = the hard flat turn, `YawsteryActionExecutor`, `steerDirection ±1`, flares the outer-lane prism ×2). `R_VesselActionHandler` (tree 134–160): InputEvent 11 → `YawsteryAction-Right`, 12 → `YawsteryAction-Left`, 13 → `BoostAction`; gamepad overrides all three to `MantaAnalogTurnBoostAction`. **Two-trigger / two-stick**, no one-thumb map (`_touchActionOverrides []`).
- **Speed class — the fastest hull in the fleet**: `VesselTransformer DefaultThrottleScaler 180` (Dolphin 68, class default 50 — `MANTA_TRIGGER_BOOST_TUNNEL.md:22–24` "far faster than the rest of the fleet"), `MaxBoostMultiplier 5`, `BoostSpeedMultiplier 1 → 1.3`, `PitchScaler 50`, `YawScaler 30` (stiff), `RollScaler 100`, `RotationThrottleScaler 0.2`, `DefaultMinimumSpeed 0`; AI `ram 0 drift 0`. The Butterfly's brief records what the Manta used to be: *"what we lost when we moved the Manta from a graceful soaring vessel to a speeding bomber"* (`BUTTERFLY.md:3`).
- **What it does to prisms**: `VesselPrismController BaseScale (20, 1, 5)` — the fleet's widest "piano-key" trail (Dolphin (3, 1.5, 8)), `prismType 3`, `Gap 18`, `trailVolume 1 → 2.5` (Mass), `turnFlareMaxScale 2`, `turnUpgradeShieldsTrail 1` (Mass-5 shielded hard-turn prisms), `shielded 0`. Impactor container `MantaImpactorDataContainer` (4 prism effects, 1 crystal, 1 lifeform-crystal, NO vesselSkimmerEffects). The skimmer (`ThiefSkimmer`) does the arming and planting.
- **Camera**: `MantaCameraSettingsSO followOffset (0, 0, −30)`, `dynamicMinDistance −27 / Max −30`, `followSmoothTime 0.02`, `rotationSmoothTime 10`, `nearClip 0.3`, `farClip 12000` (asset lines 16–22); `CameraManager.prefab` FOV 60 (lines 318/504/826). At 30 u a 60° vertical FOV spans 34.6 u: the DRAWN 4.63-u wingspan is **13 % of frame height (≈145 px at 1080p)**, the 1.20-u fin-to-fin height 3.5 % (≈37 px), the 2.28-u length ≈7 % (≈71 px); the canopy (0.77 × 0.10 u) is ≈25 × 3 px and faces AWAY from the chase camera; the eye-spots (≈0.25 u ovals) are ≈8 px. Anything under ~0.03 u is sub-pixel. **The read is the wing planform + the X-tail + the glowing stripes.** The retired `RearViewCamera` (`PipCamera.prefab` at `(0, 0, +25)`, yaw 180°, `Pip.cs` default-off since 2026-09) would have shown the nose; the live look-back is `VesselRearView` at the same −30.
- **Shipped silhouette** (`ref_sheets/manta_{top,side,chase,front34}.png`, clusters §1.2): a teardrop fuselage 0.84 wide × 0.32 tall × 1.88 long with a low domed canopy at the front third, tapering to a stern that ends in three short spikes (a trident, z −1.46); two long blade wings rooted near the nose (root chord at z −0.09..+0.09) and swept hard back to tips at z −1.5 (bind) — each wing 1.28 u long × 0.09 u thick with a large oval "eye" and two dots (accent) and a lime trailing-edge stripe (N); four small fins (0.68 × 0.17 × 0.98 u) in an X at the stern, each with its own eye-spot and stripe. In game the wings are 18° further forward and the X is opened to ±31–40° (§3.2). Black body + orange eyes + blue canopy in the authoring and the card art; navy/lavender/cyan/lime in the prefab's material array.
- **Domain colour slot**: none reaches the hull (§0). The team colour is said only by the trail and the HUD.
- Docs on its look: card art `Assets/_Graphics/CardImages/Manta*.png` (256/512 px renders of this model); ability icons are placeholder silhouettes "pending art pass" (`VESSEL_HUD_AND_ELEMENTS.md:197`, `author_manta_icon_placeholders.py`); no codex brief.

---

## 6. GAMEPLAY MOUNTS (vessel space, Unity units; bone-hosted colliders are authored at 1/100 under the 100× armature; all `m_IsTrigger 0`, layer 8, each with `ImpactCollider → VesselImpactor &538077361211256076`)

| mount | host | shape / numbers (live pose) |
|---|---|---|
| **Fuselage collider** | native bone GO `chassis.003` (head `(0, 0, −0.118)`, bone → −z) | `BoxCollider center (0, 0.005, 0) size (0.007, 0.02, 0.002)` ⇒ **0.7 × 0.2 × 2.0 u** box centred at `(0, 0, −0.618)`: x ±0.35, y ±0.10, z −1.618 .. +0.382 — covers the fuselage (cluster x ±0.418, y −0.143..+0.176, z −1.461..+0.418) minus the nose tip and the canopy top |
| **Wing colliders** | `Wing.L` / `Wing.R` (heads `(∓0.992, 0, +0.057)`, bone dir `(∓0.783, 0, −0.622)`) | `center (0, 0.007, 0) size (0.004, 0.02, 0.001)` ⇒ **0.4 wide × 2.0 along the bone × 0.1 thick**, centred 0.7 u out at `(∓1.540, 0, −0.378)`, spanning from `(∓1.227, 0, −0.130)` to `(∓2.323, 0, −1.000)` along the DRAWN wing (the bind-pose wing would sit 18° aft of it) |
| **Fin colliders** | `WingB.L/R` (heads `(∓0.553, +0.379, −1.066)`, dir `(∓0.515, +0.256, −0.818)`), `WingT.L/R` (heads `(∓0.565, −0.343, −1.066)`, dir `(∓0.494, −0.258, −0.830)`) | `center (0, 0.005, 0) size (0.003, 0.007, 0.001)` ⇒ **0.3 × 0.7 × 0.1 u** centred 0.5 u along each fin: upper pair at `(∓0.811, +0.507, −1.475)`, lower pair at `(∓0.812, −0.472, −1.481)` |
| **Near-field skimmer** `ThiefSkimmer` | nested `Skimmer.prefab` under `OrientationHandle`, scale **20** | `SphereCollider` trigger r 0.5 ⇒ **10 u radius** at the origin (Space scales the nested `Scale` 20 → 40 ⇒ 20 u); `onSkimmerShipImpact → EventOnSkimmerVesselCollision`; `VesselStatus._nearFieldSkimmer` and `VesselPrismController.skimmer` point at it; `_farFieldSkimmer` none |
| **Tail** (`VesselTail.prefab`) | vessel root | `(0, 0, −31.5)` = −1.05 × 30 (`VESSEL_TAIL_AND_JETS.md` §4 row Manta: camera −30, tail −31.5, width× 1.5) |
| **Jets** | none — `VesselTailAndJets._trails []` | `TAIL_AND_JETS.md` §5: "the Manta family shares one rig whose authored scales do not reconcile … its wing colliders are 0.004 units"; jets wait for art that answers "where is the engine". The measured answer today: the engine glow is slot 3's stripes along the wing/fin trailing edges (z −1.45..−0.11); a replacement should give the stern a real nozzle (or the three spikes) and name the node so a jet can `mountBone` it |
| **Bomb bay / weapon** | virtual (`MantaStingActionExecutor` on `ShipActions/YawsteryActionExecutor` GO; `bayResourceIndex 0`, `ResourceSystem.Resources[0] = Bombs`, max 1) | bombs are planted by skimmer contact and bloom at the TARGET; nothing is spawned from a hull point |
| **Guns / passives** | none; `Passives` GO absent | |
| `RearViewCamera` | nested `PipCamera.prefab` at `(0, 0, 25)` rot y 180° | retired (default-off) |
| Rigidbody | root: mass 1, no gravity, drag 0 (lines 2–22 of the Rigidbody block); the inactive mantis copy's six wing/fin GOs carry extra Rigidbodies (dead) | |

Every collider is a child of a BONE at a fixed local offset, so a replacement that moves a bone head moves its collider; the fin boxes additionally depend on the prefab's re-posed hold rotations (§3.2).

---

## 7. IN-FLIGHT WORK (`git fetch origin` + `who_works_where.py query` on the prefab, both FBX, the driver and the controller, 2026-10-17)

- **In flight: none.** No unmerged branch touches any of the five files in the last 30 days.
- **Landed on bleeding-edge**: `#1067 claude/serene-edison-lfv24f` (2026-10-13, merged by Shombith). Last commits per file (history was flattened, so dates are of the squash, not the art): prefab `3fe9bc569 2026-10-12 fix(ipad): cameras keep the 16:9 width…` (one line: `m_ScreenMatchMode 0 → 1` on the HUD canvas) and `1727c4744 2026-10-05 feat(vessel-audio)`; both FBX, the driver and the controller `f7ce592bb 2026-10-04`; the rigged `.meta` `376b525b1 2026-10-08 feat(crystal): bake crystal hull fusions` (Garrett — that commit also added the four `Manta_*_HullFusionBake.asset`, the ability map and the card images).
- Local branch vs `origin/bleeding-edge` on all five files: **empty diff**.
- **Talk to**: Shombith (the one landed merge). Garrett owns the fusion bakes that must be redone after a swap (§2). Nobody has model work in flight; the Falcon/Shrike/Termite copies ride along with any in-place overwrite.

---

## 8. CONSTRAINTS FOR A REPLACEMENT (numbered, exact)

1. **File**: write **in place** over `Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx`, keeping its `.meta` (guid `77a38b23d33b195469fe407e2207bbdf`, `useFileScale 1`, `globalScale 1`, `bakeAxisConversion 0`, `optimizeGameObjects 0`, `maxBonesPerVertex 4`). Binary FBX 7400. Leave `mantis_shapekey_with_animations.fbx` alone (its clips feed nothing live; its mesh is what the fusion bakes name, §2) — or retire both the dead copy and the bakes' reference in one editor pass with Garrett.
2. **Node names to keep (exact)**: Model node **`manta`** (the Mesh fileID), geometry `Sphere.018`, armature `Armature.002`, bones `chassis, chassis.001, Winghold.L, Wing.L, chassis.003, WingThold.L, WingT.L, WingBhold.L, WingB.L, WingThold.R, WingT.R, WingBhold.R, WingB.R, Winghold.R, Wing.R` with the SAME parent chain (§1.2) and **the same 15 skin clusters in this order** (the prefab's `m_Bones` array is native and will not be regenerated). One mesh, one armature; no second mesh node (`CollectElementShapes` and `FindHullRenderer` take the first/largest).
3. **Bone rest TRS**: keep the FBX's values bit-close (`chassis T(0, 0.4685, −0.00023) R(0,0,180)`, `chassis.001 T(0,1,0) R(0,0,180)`, `Winghold.* T(0, 0.6491, 0) R(0,0,∓82.05)`, `Wing.* T(0, 1.008, 0) R(0,0,±49.05)`, `chassis.003 T(0, 0.6491, 0)`, `WingThold.* T(0, 0.6454, 0) R(8.10, ±5.10, ∓64.39)`, `WingT.* T(0, 0.7266, 0) R(−2.57, ±1.94, ±31.42)`, `WingBhold.* R(−12.27, ∓7.61, ∓63.46)`, `WingB.* T(0, 0.7359, 0) R(2.31, ∓3.35, ±30.92)`; probe lines 7–21), because the prefab composes the NEW bindposes with its OLD native bone transforms: any rest change shows up as a pose change of that part by exactly the delta. Bone HEADS (vessel space, bind): `chassis (0,0,−0.469)`, `chassis.001 (0,0,+0.531)`, `Winghold/chassis.003 (0,0,−0.118)`, `Wing.* (∓0.998, 0, −0.257)`, `*hold.* (0,0,−0.763)`, `WingT.* (∓0.653, +0.102, −1.066)`, `WingB.* (∓0.653, −0.155, −1.066)`; the colliders hang off these (§6).
4. **Skin**: 7 rigid clusters (one bone per part, weights 1.0 — the puppetry rotates parts rigidly; soft blends across the wing root or fin root will look like rubber with the 60°/unit sweep); `≤4 bones/vertex`.
5. **Takes to keep**: none are bound on the live hull. If you re-export them for documentation keep the names `Armature.002|cut, pitch down, pitch up, roll left, roll right, speeddown, speedup, yaw left, yaw right` (MOTIONS, 0.833 s, 21 keys; the mantis `.meta` slices frame 19–20 as the POSE, deflections in §3.3: pitch ±24°, yaw ±21°, roll tips ±1.6 u, speedup tips −0.59 z/+0.71 inboard, speeddown +1.25 z). Export **no blend-shape curves**. Do not add a controller to the live Animator — the C# puppetry owns the poses; its amplitudes are the prefab's 27/30/60.
6. **Blend shapes**: exactly four, named `Mass`/`charge`/`space`/`time` (case-insensitive; keep the tails), last-frame weight 100, with normals; travel 5–15 % of the 4.60 diag per §4.1; morphed geometry must stay inside the intent of the fixed collider boxes (§6).
7. **Material slots, in this connection order, by name**: 0 `accent` (eye-spots, the `FlareBody` slot — give it enough area to carry a colour read, ≥10 % of the surface), 1 `BASE` (body), 2 `windsheild` (canopy — the shipped 38 polys / 0.40 u² is a real canopy, keep or grow it), 3 `N` (the boost-glow stripes, `FlareEngine`). The prefab's native array `[BlueBase, Screen, GreenAccent, Lime]` is one slot rotated against this order (§1.3) — fix the prefab array (or add `_domainReplacesMaterials = [the slot-0 material]` to `VesselCustomization` with the SMR GO `manta` in `_shipGeometries`) in the same change if the domain colour is to reach the hull at all; that is a prefab edit, flag it (`Affects: Garrett's fusion bakes, Shombith's #1067 area`).
8. **Scale/axes to export with**: reproduce the shipped convention exactly — `UnitScaleFactor 1.0` (cm), mesh node `manta` at **identity with vertices in cm** (hull ±197.6 in mesh-local), `Armature.002 T(0,0,0) R(−90,0,0) S(100,100,100)`, FBX Y-up, nose along FBX world **+Z** (= Blender −Y before the node rotation), starboard at FBX world **−X** (Unity mirrors X), `.L` bones on FBX +X. Verify with `fbx_probe`: `manta [Mesh] T=(0,0,0) R=(0,0,0) S=(1,1,1)`, `Armature.002 R=(−90,0,0) S=(100,100,100)`, bounds `(−197.6, −35.74, −185.7)..(197.6, 22.79, 41.77)`, `material slots {0:…,1:…,2:…,3:…}` in the order above, `skin: 15 clusters`.
9. **Hull bounds to match** (vessel space, bind): x ±1.976, y −0.357..+0.228, z −1.857..+0.418; fuselage inside x ±0.418, y −0.143..+0.176, z −1.461..+0.418 (collider x ±0.35, y ±0.10, z −1.62..+0.38); wing tips at `(∓1.976, 0, −1.5)` so the drawn tips land at `(∓2.315, 0, −0.81)` under the prefab's 18° hold offset; fin tips at z −1.857, |x| 1.15–1.18, y +0.228 (T) / −0.357 (B); canopy on the top at z −0.28..+0.38. Keep the origin 0.418 u behind the nose — the camera (−30), tail (−31.5), skimmer (r 10 at the origin) and every collider assume it.
10. **Forward axis**: Unity +Z = FBX world +Z; up +Y. Rest pose = the ship's frame; bone parent frames must keep the orientations in §3.1's table or the puppetry's terms land on different physical axes.
11. **Wiring path — recommended: in-place overwrite** (same guid, same `manta` Model node, same 15 clusters in order) = **zero prefab edits** on Manta, Falcon, Shrike and Termite. Risks: (a) the four `Manta_*_HullFusionBake.asset` (and the Falcon/Shrike/Termite ones) name the mantis mesh and are already reference-mismatched against the drawn hull — re-bake in the editor against the live mesh (hand task, Garrett); (b) a changed cluster count/order silently breaks the skin (Unity logs a bones/bindposes mismatch and draws nothing or garbage) — `fbx_probe` must show `skin: 15 clusters` and the `bones (15)` list in the shipped order; (c) a renamed/added material rotates submeshes — confirm `materials=['accent','BASE','windsheild','N']`; (d) `optimizeBones 1` strips a bone that skins nothing and has no skinning descendant — keep the hold bones as parents of skinning bones; (e) the prefab's re-posed holds (§3.2) will deform a replacement exactly as they deform the shipped art; model the wings/fins so that the +18° / fin-swap pose is the intended look, or reset the four hold quaternions in the prefab (prefab edit); (f) in-editor re-import is the one thing offline cannot prove (fileID regeneration, blend-shape import, the avatar error) — a human reimport + `FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs` + `Audit Vessel Construction` + a crystal pickup in Play mode. The alternative (new file + editor swap) means re-authoring the 15-entry `m_Bones` array, seven part references, seven `_shipGeometries`, the SMR mesh/avatar and four sibling prefabs by hand — strictly more risk, no benefit.

---

## 9. QUALITY BAR

**Keep (what the shipped model gets right)**
- Topology hygiene: 7 closed manifold shells, 0 boundary edges, 0 non-manifold, 0 zero-area, 92 % quads, UVs packed in 0..1 — the cleanest mesh in the measured fleet; it would take a texture.
- Four REAL morphs (12.0 / 7.3 / 17.8 / 15.6 % of diag), two of them already in the right vocabulary (`space` extends the span +0.64 u, `time` sweeps the hull −0.58 z).
- A rig the code already fits: 7 rigid parts with hold bones at the fuselage so sweep/flap/twist pivot at the roots; the wing-sweep puppetry (60°/unit) reads at 30 u.
- A strong planform from the chase camera: long swept blades with eye-spots, an X-tail, a real canopy, a trident stern; 3.95–4.63 u across against a 34.6-u frame.

**Weak (what to beat)**
- **Flat**: 0.585 u tall at bind (fins), the fuselage only 0.32 u tall and 0.84 wide; from the chase camera the body is a 25 × 10 px sliver and the canopy 25 × 3 px — the Manta has no volume to read as a "nuke". Wings are 0.088 u thick slabs.
- **Material array rotated** (§1.3): the body wears the Window material, the canopy the accent, the eye-spots dark navy; the domain colour never lands; `FlareBody` would light the eye-spots navy. The artist's authoring (black / orange eyes / blue canopy, the card art) is not what the game draws.
- **Morph semantics**: `Mass` slides the wings forward (reads as reach, not volume); `charge` is a 4 % general spread (invisible at 30 u); `space` droops the wings −0.2 y (reads as sag).
- The prefab's re-posed holds (§3.2) mean the FBX's rest is not the drawn rest, the fins are drawn vertically swapped, and the hold names are inverted against the FBX — a trap for anyone measuring from the file.
- 221 n-gons (up to 17-gons), fan-triangulated; the fins are 4 × 456 verts for 0.68-u blades (over-spent) while the fuselage is 453 verts (under-spent).
- A dead duplicate hull (1.1 MB mantis file, inactive native copy with 6 Rigidbodies, an Animator with the controller) that the morph discovery, the hull-fusion baker and the audits still see; the live Animator has no controller and a failed avatar.
- No engine node: no jets (TAIL_AND_JETS §5); the boost read is a lime stripe on the trailing edges.

**Measured polycount**: 3,156 polys / 6,746 tris / 3,351 verts (7,119 imported). **Target for the replacement**: the hull reads at ≈145 px wide and ≈37 px tall from the shipped camera (§5); **6,000–12,000 triangles** (quads/tris only, no n-gons, one closed shell per rigid part, smooth-shaded with hard edges only on the wing leading/trailing edges and the canopy rim), hard ceiling 15,000 (this hull is drawn four times over in the fleet — Manta, Falcon, Shrike, Termite — and the Dolphin's 25k is the mobile reference). Spend it on the fuselage volume and the bomb-bay stern (the things Charge and Mass must morph), the canopy and the wing root fillets; not on the fins' flat faces.
