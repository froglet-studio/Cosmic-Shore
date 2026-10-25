# Serpent — MODEL DOSSIER (measured 2026-10-17, branch ccr-d93a6dfb-ugbxng; `git diff HEAD origin/bleeding-edge` is empty for every file named here)

> **Record of the ARTIST file as shipped before 2026-10-17.** On 2026-10-17 the file this dossier measures was
> rebuilt in place by `python3 Tools/Build/vessel_model/build.py serpent` (spec `Tools/Build/vessel_model/specs/serpent.py`,
> skill `/vessel-model`): same path and guid, same node, geometry, bone and material names, every bone's rest pose
> reproduced to 0.000 (`rest_compare.py`). Sections 1-7 describe the artist file the spec was written against and
> stay the explanation of WHY the spec keeps each name and number; section 8 is the contract the rebuilt file meets.
> Paths under `<scratchpad>/` were the measuring session's temporary files: the commands beside them regenerate
> every number from the repo.


Written for the modeller who will REPLACE the Serpent's model with a better one on the same prefab, same gameplay, same code.
Every number was measured from the asset or the code; where a doc disagrees with an asset the asset is reported and the doc named.
Scratch evidence (all under `<scratchpad>/`): `probe/SerpentExport.txt`, `probe/serpent.txt` (fbx_probe --takes), `serpent/prefab_tree.txt` (annotated prefab hierarchy; script `serpent/prefab_tree.py`), `serpent/mods.txt` (every PrefabInstance override grouped by target; `serpent/mods.py`), `serpent/bpy_measure.txt` (Blender 5.2 import: per-slot / per-bone / per-shape geometry; `serpent/bpy_measure.py`), `serpent/bpy_takes.txt` (take key values + posed evaluation; `serpent/bpy_takes.py`), renders `renders/serpent_live.png` (Cycles, Down pose, rear-right-above), `renders/serpent_rest_{front_from_nose,side_from_right,top,threequarter}.png` (Cycles, rest pose, orthographic; `serpent/render_rest.py`).

**Three frames are used, and the conversions were PROVED, not assumed:**
- **file frame** (Blender/FBX, `up=+2 front=-1` = Z up, −Y front declared; probe line 3): **nose = +Y_b**, right = +X_b, up = +Z_b (reasoning in §1.1).
- **model-root frame** (Unity's imported `SerpentExport` GameObject, before the prefab's own transform): `(x_m, y_m, z_m) = (−x_b, z_b, −y_b)`. Proof: the prefab's bone-local overrides equal the FBX bone-local `Lcl Translation` with X negated — BotTail1.L FBX `T=(2.566, −1.036, 1.134)` ↔ prefab target 7384329532987323624 `(−2.5659, −1.0363, 1.1339)`; HeadTail1.L `(−0.1688, −1.412, −2.197)` ↔ `(0.1688, −1.4123, −2.1972)` (`mods.txt`). And the prefab's rig objects, which sit in the "Rig 1" frame (= model root rotated 180° about Y), read `(x_b, z_b, y_b)`: `HeadTailRig5R (2.650, 6.008, −8.048)` ↔ Blender `HeadTail5.R head (2.650, −8.048, 6.008)` to 3 decimals (`prefab_tree`/§2 rig listing vs `bpy_measure.txt` BONES).
- **vessel frame** (the `Serpent` prefab root: what `VesselTransformer` flies, +Z forward): the FBX instance root carries `m_LocalRotation (0, −1, 0, −2.5e−7)` = 180° about Y and `m_LocalScale (2, 2, 2)` (`mods.txt` target −8679921383154817045), so **`(x_v, y_v, z_v) = (2·x_b, 2·z_b, 2·y_b)`** — nose +Z_v, right +X_v, up +Y_v. Cross-check: `VesselTail` sits at `z_v = −262.5` (behind) and `SingleStickVesselTransformer` rotates about `courseTransform.right/up` and moves along `transform.forward` (+Z).

---

## 1. LIVE MODEL

| fact | value | measured at |
|---|---|---|
| prefab | `Assets/_Prefabs/Spacevessels/Serpent.prefab` (5,296 lines) | `wc -l` |
| **live FBX (drawn)** | `Assets/_Models/Vessel Models/SerpentExport.fbx`, 1.2 MB, FBX 7400 | probe line 1 |
| guid | `7fad38a2fd9fce64088bb244e1473ffe` (216 occurrences in the prefab, all inside one PrefabInstance block and its 29 `stripped` stubs) | `.meta` line 2; `grep -c` |
| how it is drawn | nested `PrefabInstance &6541039741583580943` (prefab lines 3577–4515), `m_SourcePrefab {fileID: 100100000, guid: 7fad…}` (line 4515), `m_TransformParent {fileID: 5928382658856804067}` = the **`Serpent` root Transform** itself (GO 6417075533431866457). One `SkinnedMeshRenderer` (stub 4172724446676456733, cso 7146665307521698322) on the `SerpentMesh` node. No direct mesh refs. | `prefab_tree.txt` lines 1–30; `mods.txt` |
| instance transform | root Transform (cso −8679921383154817045): **pos (0,0,0), rot (0,−1,0,−2.5e−7) = 180° about Y, scale (2,2,2)**, `m_ConstrainProportionsScale 1`, euler hint (0,−180,0). Root GO (cso 919132149155446097) `m_Name = SerpentExport`, `m_IsActive 1`. Plus **one `m_LocalScale (1.5,1.5,1.5)` override on FBX transform `−2193820272513555211`** — NOT one of the 25 referenced (stripped) transforms, so it is one of `SerpentArmature`, `SerpentMesh`, `EngineBone`, `ChassisBone`, `BotTail5.L`; sub-asset fileIDs are path hashes and cannot be derived offline, and git cannot date it (the whole SerpentExport wiring arrived in one squashed merge, #999 2026-10-07). **Open it in the editor (Overrides dropdown) before trusting any size below**: if it is `SerpentArmature` the whole hull is ×3 in game (not ×2) and the collider is not; if `SerpentMesh`, only the BoxCollider and renderer bounds are ×1.5; if a bone, that bone's skinned patch is ×1.5. My renders show the file at scale 1. | `mods.txt`; `git log -S'-2193820272513555211'` |
| **the nested `serpent.fbx` is NOT drawn** | `PassiveEffects` GO (pos (878.5386, 562.8763, 7.433295)) hosts `PrefabInstance &8968102816825443992` of `serpent.fbx` (guid `db4a9f6d016060442900a5a834df1d7c`, 41 refs). Its overrides: root GO `m_Name serpent`, **`m_IsActive 0`**, root transform pos `(−878.5386, −562.8763, −7.433295)` (cancels the parent), 16 `m_Materials.Array.data[0]` overrides (13× GreenAccentVesselMaterial, 1× ScreenVesselMaterial on the windshield, 1× LimeVesselMaterial, one 3-slot renderer × BlueVesselMaterial), one `m_AABB` override. **No MeshFilter/MeshRenderer stub from this guid exists in the prefab** — the prefab's only MeshFilter is the VacuumSkimmer's built-in sphere (fileID 10207). So `Docs/VESSEL_CONSTRUCTION.md:40,64` ("its single `MeshFilter` draws `serpent.fbx`… so the static half does not [morph]") is wrong about liveness: the old Maya model is an inactive vestige. | `mods.txt` block 5; `prefab_tree.txt` |
| other nested prefabs | `VesselTail.prefab` (guid ef25530d…) at `(0,0,−262.5)` `widthScale 12.5`; `PipCamera.prefab` → `RearViewCamera` under `OrientationHandle` at `(0,0,25)` rot 180° Y, `m_IsActive 0`; `SerpentHUDVariant.prefab` and `NotificationPresenter.prefab` under `ShipHUDContainer` (scale 0). | `mods.txt` |

### 1.1 Units, scale, axes (measured)

- FBX `GlobalSettings`: **`UnitScaleFactor = 100.0`** (1 file unit = 100 cm = 1 m), `up=+2` (Z), `front=−1` (−Y), `coord=+0`, TimeMode 10 (probe line 3). `.meta`: `globalScale 1`, `useFileScale 1`, `useFileUnits 1`, `bakeAxisConversion 0`, `optimizeGameObjects 0`, `isReadable 1`, `importBlendShapes 1`, `animationType 2` (Generic), **`rootMotionBoneName: HeadRootBone`**, `fileIdsGeneration 2`, `materialImportMode 2`, `materialLocation 1`, `materialSearch 1`, six `clipAnimations` with pinned `internalID`s (§3).
- **File scale is 1.0, not 0.01.** The orchestrator's note "UnitScaleFactor 100 means cm, Unity = FBX/100" is wrong for this file: Unity's file scale is `UnitScaleFactor/100 = 1`, and the prefab proves it — bone `HeadTail2.R` FBX `T.x = 1.681` lands as `m_LocalPosition.x = −1.6807` (target −6309912351021498685), not 0.0168. (Contrast `serpent.fbx`: `UnitScaleFactor 1.0`, mesh bounds ±175 → that file imports at 0.01.)
- Both file root nodes (`SerpentMesh [Mesh]`, `SerpentArmature [Null]`) are at identity; the Z-up→Y-up conversion is applied by Unity (`bakeAxisConversion 0`), giving the model-root frame above.
- **Hull bounds, file frame** (rest pose, mesh `Cube.190`): x `[−4.202, 4.202]`, y `[−10.103, 1.487]`, z `[−5.179, 6.143]`, diag **18.253** (probe line 35; `bpy_measure.txt` line 3; `measure_vessel_models.py`: extent (8.404, 11.591, 11.322)).
- **Hull bounds, vessel frame (Unity units, scale 2 applied, the ×1.5 override NOT applied)**: **x `[−8.404, +8.404]`, y `[−10.358, +12.286]`, z `[−20.206, +2.974]`** → **16.81 wide × 22.64 tall × 23.18 long, diagonal 36.51**. The vessel origin sits inside the head; only 2.97 units of hull lie ahead of it and 20.2 behind.
  - head alone (HeadRootBone cluster, 632 verts): x ±3.33, y `[−3.38, 4.06]`, z `[−5.02, 2.97]` (6.7 × 7.4 × 8.0)
  - compact body (head + chassis + engine): x ±4.03, y `[−4.13, 4.13]`, z `[−10.87, 2.97]` (8.1 × 8.3 × 13.8)
  - window: x ±3.33, y `[−1.74, 0.96]`, z `[−4.58, 2.97]` (the front face)
  - engine pads (slot 1): centroid `(0, 0.21, −8.70)`, x ±3.36, y `[−1.41, 1.83]`, z `[−9.30, −8.31]`
  - tendril tips: HeadTail5.R centroid `(6.93, 11.86, −18.36)`, BotTail5.R `(7.47, −8.75, −18.66)` (L mirrored in x)
- **Nose axis = file +Y_b = vessel +Z_v.** Reasoning from geometry (`bpy_measure.txt`): the Window slot (80 polys, the visor) spans y_b `[−2.288, 1.487]` — the +Y extremity; the Engine pads face y_b `[−4.65, −4.16]` behind the head; the four tendril tips (HeadTail5/BotTail5, y_b ≈ −9.2…−9.3) trail to −Y; HeadTail tips are at z_b +5.9 (above), BotTail tips at −4.4 (below). The prefab's 180° flip then turns the model-root −Z nose into vessel +Z, consistent with the tail at −262.5 and the transformer's +Z.

### 1.2 Node hierarchy (FBX, exact names; `[skins N]` = vertices whose weight-argmax is that bone; head = bone head in VESSEL frame `(2x_b, 2z_b, 2y_b)`; local T/R = FBX rest `Lcl Translation/Rotation`)

```
SerpentMesh      [Mesh]  T(0,0,0) R(0,0,0) S(1)   geometry Cube.190, materials [Body, Engine, Window, Domain]
SerpentArmature  [Null]  T(0,0,0) R(0,0,0) S(1)
  HeadRootBone        head (0, 0, −4.14)     T(0,−2.071,0) R(180,0,90)          skins 632   head + visor (slots 0/3/2: 352/198/80 polys)
    EngineBone        head (0, 0, −5.39)     T(−0.624,0,0) R(0,180,0)           skins 578   engine housing + the 3 green pads (slot1 144, 0: 320, 3: 112)
    ChassisBone       head (0, 1.53, −5.01)  T(−0.436,0,−0.767)                  skins 666   rear carapace plate (0: 416, 3: 248); x±4.0 y±4.1 z[−10.9,2.1]
    HeadTail1.R       head (2.83, 4.39, −4.48)  T(−0.169,1.416,−2.197) R(0,157.9,0)  skins 46
      HeadTail2.R     head (2.83, 5.66, −7.59)  T(1.681,0,0) R(−1.28,−2.61,52.14)    skins 10
        HeadTail3.R   head (5.42, 6.56, −9.40)  T(1.640,0,0) R(2.49,−12.29,−47.62)   skins 22
          HeadTail4.R head (5.75, 9.34, −13.07) T(2.312,0,0) R(−0.38,−5.16,−10.06)   skins 18
            HeadTail5.R head (5.30, 12.02, −16.10) T(2.030,0,0) R(2.82,39.32,51.26)  skins 499  the ringed TIP bulb
      HeadTailSpike.R head (2.83, 5.66, −7.59)  T(1.681,0,0) R(−14.43,53.02,−40.83)  skins 17
    HeadTail1.L … HeadTail5.L, HeadTailSpike.L   mirror (x → −x)                      46/9/22/18/499/17
    BotTail1.R        head (2.05, −2.27, 0.99)  T(2.566,1.023,1.134) R(3.09,−157.2,−7.43)  skins 17
      BotTail2.R      head (2.45, −3.57, −2.08) T(1.681,0,0)                          skins 53
        BotTailSpike.R head (4.19, −5.14, −7.19) T(2.810,0,0) R(29.4,−47.24,−68.49)   skins 15
        BotTail3.R    head (4.19, −5.14, −7.19) T(2.810,0,0) R(2.95,2.24,14.7)        skins 36
          BotTail4.R  head (6.51, −6.40, −10.58) T(2.148,0,0) R(−7.70,−13.95,−31.36)  skins 16
            BotTail5.R head (6.67, −7.11, −17.14) T(3.298,0,0) R(21.24,34.38,30.24)   skins 499  the ringed TIP bulb
    BotTail1.L … BotTail5.L, BotTailSpike.L      mirror                               19/49/37/16/499/16
```
27 bones (`bones (27)` probe line 50), 27 skin clusters, no unskinned vertex (`unskinned verts: 0`). **Every bone descends from `HeadRootBone`**, so a HeadRootBone rotation moves the whole hull (§3). Bone lengths: HeadTail chain 1.681/1.640/2.312/2.030/2.030; BotTail chain 1.681/2.810/2.148/3.298/3.298 file units (`bpy_measure.txt` BONES).

### 1.3 Polycount, materials, UVs

- **4,286 polygons, all quads (0 tris, 0 ngons) = 8,572 triangles; 4,372 vertices** (probe line 35; bpy). Vertex budget: the four tip bulbs hold **4 × 499 = 1,996 vertices (45.7 % of the mesh)**; head 632, chassis 666, engine 578; the 20 chain/spike bones share ~500.
- **Material slots (FBX connection order = Unity submesh order; `ByPolygon`)**, with area in file units²:

| slot | FBX name | polys | tris | area | FBX Phong diffuse / emissive | Unity material (`.meta externalObjects` by NAME) |
|---|---|---|---|---|---|---|
| 0 | `Body` | 2,032 | 4,064 | 124.86 (64.3 %) | (0.046,0.037,0.049) near-black / (1,1,1) | `BlueBaseVesselMaterial.mat` (54e78ce1…) |
| 1 | `Engine` | 144 | 288 | 3.59 (1.8 %) | (0.005,0.80,0) green / (1,1,1) | **no remap** → importer-generated (see §5 note on `Engine_2.mat`) |
| 2 | `Window` | 80 | 160 | 11.28 (5.8 %) | (0,0,0) / (1,1,1) | `ScreenVesselMaterial.mat` (923d3665…) |
| 3 | `Domain` | 2,030 | 4,060 | 54.46 (28.0 %) | (0.80,0.70,0.0005) yellow / (1,0.054,0.091) red | `GreenAccentVesselMaterial.mat` (539a8c65…) |

  All three shipped `.mat`s use shader guid `0236447d5f06b0541a43756d85b5f222` with `_ColorMultiplier 1`, `_BaseColor (1,1,1)`. **This slot order is NOT the Squirrel's (Domain/Body/Window/Engine = 0..3) — on the Serpent `Body` is slot 0 and `Domain` is slot 3**, which inverts what the code does with slots 0 and 3 (§2, §3.5).
- **UVs**: one layer `UVMap`, u `[0, 1]`, v `[0, 1.001]`, 284 of 17,144 corner UVs outside 0..1 by ≤ 0.001 (negligible). Normals present; `normalImportMode 0` (import), `normalSmoothAngle 60`, `blendShapeNormalImportMode 1`.

---

## 2. WHAT THE PREFAB REFERENCES INSIDE THE FBX

29 `stripped` stubs carry guid `7fad…`; each stub's `m_CorrespondingSourceObject` (cso) is the FBX sub-asset. Who references each stub (`prefab_tree.py` referrer scan):

| stub (prefab fileID) | cso in FBX | type | node (by reasoning) | referenced by |
|---|---|---|---|---|
| 6198951092470964830 | 919132149155446097 | GameObject | **asset root `SerpentExport`** | `RigBuilder` (added component 4812916991114041632, script fff0960e…) `m_GameObject` |
| 6722969896632727780 | −8679921383154817045 | Transform | root transform | `Serpent` root `m_Children[]`; added GO **`Rig 1`** (tr 1937187212833463716, rot 180° Y) `m_Father` |
| 841299382724584094 | 5866666021909216657 | **Animator** (on the root GO) | — | `MantaAnimationContoller.animator`; overrides: `m_Controller = SerpentAnimController.controller` (c758fbec…), `m_ApplyRootMotion 1`, `m_CullingMode 0`, `m_Enabled 1` |
| 3878638393738004476 | −1218854902558664461 | **GameObject `SerpentMesh`** | the skinned mesh node | `VesselCustomization._shipGeometries[0]`; added **BoxCollider** 2203058038973014175 and **ImpactCollider** 8026890533229392904; override `m_Layer 8` |
| 4172724446676456733 | 7146665307521698322 | **SkinnedMeshRenderer** | on `SerpentMesh` | `MantaAnimationContoller.SkinnedMeshRenderer`, `CloakSeedWallActionExecutor.shipRenderer`; overrides `m_BlendShapeWeights[0..3] = 0`, `m_Enabled 1`. **No `m_Materials` override** — materials come from the `.meta` remap |
| 1547726606227589206 | −3477674082847123623 | Transform | **`HeadRootBone`** (the common `m_Source` of all four chain roots; no TRS override) | `HeadTailRig1L/1R`, `BotTailRig1L/1R` DampedTransform `m_Source` |
| 404141260610032105 | 6871856040060653286 | Transform | `HeadTail1.R` | `HeadTailRig1R` constrained; `HeadTailRig2R` source |
| 8262065554035974604 | −6309912351021498685 | Transform | `HeadTail2.R` | `HeadTailRig2R` constrained; `HeadTailRig3R`, `HeadTailRigSpikeR` source |
| 4131017084782885619 | −2048536552325192196 | Transform | `HeadTail3.R` | `HeadTailRig3R` constrained; `HeadTailRig4R` source |
| 2465166422325552180 | 8714596335059352379 | Transform | `HeadTail4.R` | `HeadTailRig4R` constrained; `HeadTailRig5R` **and `BotTailRig5L`** source |
| 6844109668120184998 | −8917873253069645911 | Transform | `HeadTail5.R` | `HeadTailRig5R` **and `BotTailRig5L`** constrained |
| 5691391964006014378 | −7764889467141619035 | Transform | `HeadTailSpike.R` | `HeadTailRigSpikeR` constrained |
| 1922377479407412320 | −4581329752982848657 | Transform | `HeadTail1.L` | `HeadTailRig1L` constrained; `HeadTailRig2L` source |
| 2544048065104769835 | 8757309679301536804 | Transform | `HeadTail2.L` | `HeadTailRig2L` constrained; `HeadTailRig3L`, `HeadTailRigSpikeL` source |
| 6162625208391253724 | −8124359566205230637 | Transform | `HeadTail3.L` | `HeadTailRig3L` constrained; `HeadTailRig4L` source |
| 6087255431018261798 | −8161588582316477911 | Transform | `HeadTail4.L` | `HeadTailRig4L` constrained; `HeadTailRig5L` source |
| 1459916229348879670 | 5657887749481935417 | Transform | `HeadTail5.L` | `HeadTailRig5L` constrained |
| 1250774945935784271 | −3774625812452956608 | Transform | `HeadTailSpike.L` | `HeadTailRigSpikeL` constrained |
| 6663070525935420530 | −8741051837592052867 | Transform | `BotTail1.R` (pos (−2.566, 1.023, 1.134)) | `BotTailRig1R` constrained; `BotTailRig2R` source |
| 2099799404058145160 | −4042670523334054265 | Transform | `BotTail2.R` | `BotTailRig2R` constrained; `BotTailRig3R`, `BotTailRigSpikeR` source |
| 6476900961287527361 | 226556460406965454 | Transform | `BotTail3.R` | `BotTailRig3R` constrained; `BotTailRig4R` source |
| 8810983775067739934 | 2342038874561824785 | Transform | `BotTail4.R` | `BotTailRig4R` constrained; `BotTailRig5R` source |
| 7028248899988346343 | −4949689232603444504 | Transform | `BotTail5.R` | `BotTailRig5R` constrained |
| 3603840335276920898 | 7549468172377296717 | Transform | `BotTailSpike.R` | `BotTailRigSpikeR` constrained |
| 4376399786491767783 | 7384329532987323624 | Transform | `BotTail1.L` (pos (−2.566, −1.036, 1.134)) | `BotTailRig1L` constrained; `BotTailRig2L` source |
| 5464831297355688491 | −7993735538511186652 | Transform | `BotTail2.L` | `BotTailRig2L` constrained; `BotTailRig3L`, `BotTailRigSpikeL` source |
| 4325684030037012268 | 7404368904021274659 | Transform | `BotTail3.L` | `BotTailRig3L` constrained; `BotTailRig4L` source |
| 2669413742067869260 | 9209255795966687555 | Transform | `BotTail4.L` | `BotTailRig4L` constrained |
| 8728397765940988450 | −6636318514146405075 | Transform | `BotTailSpike.L` | `BotTailRigSpikeL` constrained |

- **Unreferenced FBX nodes** (no stub): `SerpentArmature`, the `SerpentMesh` Transform, `EngineBone`, `ChassisBone`, `BotTail5.L`. One of them carries the `m_LocalScale 1.5` override (§1). `BotTail5.L` is the ONLY tendril bone without a damping constraint (see the mis-wire below).
- **The rig the prefab adds under the FBX root** (`m_AddedGameObjects`: `Rig 1`, GO 6487719315442839558, `Rig` component 3951856675735486313 weight 1; `RigBuilder` on the asset root with one layer → that Rig). Package `com.unity.animation.rigging` **1.3.0** (`Packages/manifest.json:11`). 27 children: **25 `DampedTransform`** (script 1b37cffa…; fields `m_DampPosition 0.4`, `m_DampRotation 0.6`, `m_MaintainAim 1`) — chain roots `HeadTailRig1L/1R`, `BotTailRig1L/1R` at `m_Weight 0.5`, every other link at 1.0, each link's source = the previous bone; `HeadRig` = a DampedTransform with **null** constrained/source (dead); `ChassisRig` (pos (0, 0.767, −2.507) rig frame = on ChassisBone) and `EngineRig` ((0,0,−2.695) = on EngineBone) are plain transforms with no constraint. **Mis-wire measured:** `BotTailRig5L` constrains `HeadTail5.R` from `HeadTail4.R` — a duplicate of `HeadTailRig5R` — so **`BotTail5.L` has no lag** (the lower-left tip snaps while the other three trail). These are prefab defects, kept verbatim by an in-place model swap; fix them in the prefab, not the model.
- **Per-bone TRS overrides**: 23 tendril bones + root carry `m_LocalPosition.*`/`m_LocalRotation.*` overrides that reproduce the FBX rest values (X negated) to ~1e−3 (`mods.txt`). They are harmless today but **they BEAT whatever rest pose a replacement FBX ships** — see §8 risk 2.
- **Exact node names a replacement must keep** (sub-asset fileIDs are path hashes — `fileIdsGeneration 2`, `internalIDToNameTable []` — so same file, same path is the only way these 29 stubs and the six clip IDs keep binding): `SerpentMesh` (the GameObject that hosts the SkinnedMeshRenderer, the BoxCollider, the ImpactCollider and the `_shipGeometries` entry), `SerpentArmature`, `HeadRootBone`, `HeadTail1.R`…`HeadTail5.R`, `HeadTailSpike.R`, `HeadTail1.L`…`HeadTail5.L`, `HeadTailSpike.L`, `BotTail1.R`…`BotTail5.R`, `BotTailSpike.R`, `BotTail1.L`…`BotTail5.L`, `BotTailSpike.L`, `EngineBone`, `ChassisBone` — **and their parent paths** (the hash is of the path, proved by the root transform cso `−8679921383154817045` and root GO `919132149155446097` being identical across `SerpentExport.fbx`, `serpent.fbx` and the Dolphin's FBX, i.e. independent of the file/root name). Keep `HeadRootBone` as the single root of the skeleton under `SerpentArmature`; keep each chain's parentage as listed in §1.2. The geometry name `Cube.190` is referenced by nothing (the SMR's `sharedMesh` lives inside the FBX prefab) and may change; the blend-shape names must stay `Space`/`Mass`/`Time`/`Charge` (any case; §4); material names `Body`/`Domain`/`Window` must stay for the `.meta` remap (add an `Engine` remap, §8).

---

## 3. ANIMATION — Animator-driven (`MantaAnimationContoller : VesselAnimation`, `Assets/_Scripts/Controller/Animation/MantaAnimationContoller.cs`)

### 3.1 The driver (lines 16–46)
- `PerformShipPuppetry(pitch, yaw, roll, throttle)` lerps four floats at `animationSpeed 3.25` and writes `animator.SetFloat("Pitch", −currentPitch × 2)`, `("Yaw", currentYaw × 2)`, `("Roll", currentRoll × 2)`, `("Throttle", currentThrottle × 2)`; `Idle()` lerps all four to 0. `hasBoost` is **0** on the Serpent (prefab &6776027772320837218), so the `Boost` bool is never set. `AssignTransforms()` is a NOOP and `ResolveParts` is not overridden: **no `ResolvePart` candidates, no `CaptureRestRotations`, no code-driven bone** — the only code-driven thing is the Animator parameters. `SaveNewPositions 1`, `brakeThreshold 0.65`, `lerpAmount 2`, `smallLerpAmount 0.7` (prefab) are base-class fields the Serpent's path never reads.
- Inputs (`VesselAnimation.Update`, lines 32–34): the Serpent is single-stick (`IsSingleStickControls`), so `pitch = EasedLeftJoystickPosition.y`, `yaw = EasedLeftJoystickPosition.x`, `roll = throttle = 0`. Hence **Roll and Throttle are always 0** and the ×2 scaling saturates the blend at **half stick** (clip positions are at ±1, the parameter reaches ±2).

### 3.2 The controller `Assets/_Animations/SerpentAnimController.controller` (guid c758fbec7faf0804f99bee5cb830a388)
- Parameters (float): `Blend` (default **1**), `pitch`, `yaw`, `roll`, `throttle`, `Pitch`, `Yaw`, `Roll`, `Throttle` (lowercase four unused). One layer `MovementAnimLayer`, one state **`SerpentBaseAnimationsBlendTree`** (default; `m_WriteDefaultValues 1`; `m_TimeParameterActive 1` with `Blend` as the time parameter).
- Motion = BlendTree `3972788959030435885` "Blend Tree", **`m_BlendType 4` (Direct)**, one child whose direct weight is `Blend` (=1): BlendTree **`2644015340837613101`**, **`m_BlendType 3` (2D Freeform Cartesian)**, X = **`Yaw`**, Y = **`Pitch`**, four children (all `m_TimeScale 1`):

| child clip (fileID in `SerpentExport.fbx`) | take | 2D position |
|---|---|---|
| −8897570794599522389 | `SerpentArmature\|SerpentUpAnim` | (0, −1) |
| 8544842894480929770 | `SerpentArmature\|SerpentDownAnim` | (0, +1) |
| 3182847114785585752 | `SerpentArmature\|SerpentRightAnim` | (+1, 0) |
| 4294602363674685341 | `SerpentArmature\|SerpentLeftAnim` | (−1, 0) |

  **`SerpentIdleAnim` and `SerpentSpeedIncreaseAnim` are not in the tree**; at (0,0) the freeform blend weights the four poses ≈ equally and the symmetric pairs cancel to ≈ rest. `Throttle`/`Roll` drive nothing. The file also holds orphan trees (not reachable from the state machine) referencing `mantis_shapekey_with_animations.fbx` (7c324ef5…), `SparrowModel1.fbx` (151e3f03…) and `urchan_shapekey_with_animations.fbx` (7f9bbc7b…) — leftovers from the controller it was copied from.
- `.meta clipAnimations` (name = takeName, `firstFrame 0`, `lastFrame 1`, `loopTime 0`, `keepOriginalPositionY 1`, `keepOriginalOrientation 0`, `loopBlendOrientation 0`): `SerpentArmature|SerpentDownAnim` **8544842894480929770**; `…IdleAnim` 6649277073756093042; `…LeftAnim` **4294602363674685341**; `…RightAnim` **3182847114785585752**; `…UpAnim` **−8897570794599522389**; `…SpeedIncreaseAnim` 102859810310046030. The controller binds the four bold IDs; because the `.meta` pins each by `takeName`, a re-export that keeps the take names keeps the binding.

### 3.3 Every take is a POSE (one key), and what each deflects (`bpy_takes.txt`; probe `--takes` lines 51–62: "0.000s … keys 1")

| take | moving node(s) | key value (bone-local) | what it does in the file frame = vessel frame |
|---|---|---|---|
| `SerpentIdleAnim` | none | identity | rest |
| `SerpentDownAnim` | `HeadRootBone` | quat (0.9659, 0, −0.2588, 0) = **−30° about bone Y** | **whole hull pitches NOSE DOWN 30°** about the HeadRootBone head (vessel (0,0,−4.14)); posed bounds z_b `[−2.54, 9.12]` (tendrils rise), y_b `[−11.46, 1.42]` |
| `SerpentUpAnim` | `HeadRootBone` | +30° about bone Y | **nose UP 30°**; posed z_b `[−8.38, 2.75]` |
| `SerpentLeftAnim` | `HeadRootBone` | euler (−25, 0, −25) | **nose yaws LEFT 25°** (tail swings to +x_b = vessel +X; posed x_b `[−2.53, 8.65]`) **plus a 25° bank, left side down** (right tips rise: HeadTail5.R z_b 6.01→6.57, BotTail5.R −3.55→−1.81) |
| `SerpentRightAnim` | `HeadRootBone` | euler (+25, 0, +25) | mirror: nose RIGHT 25°, bank right side down; posed x_b `[−8.65, 2.53]` |
| `SerpentSpeedIncreaseAnim` | `ChassisBone`, `EngineBone` | loc **−3.566** / **+1.090** along bone X | chassis plate slides **back 3.566 file units (−7.13 z_v)**, engine **back 1.090 (−2.18 z_v)** — a telescoping stern. **Not wired.** |

  Each take also carries **four blend-shape curves (`Space`/`Mass`/`Time`/`Charge`) pinned at 0** (probe "4 blend-shape curves"; bpy actions `Key|SerpentArmature|…`). `VesselAnimation.LateUpdate` (lines 450–491) is the single writer of blend-shape weights precisely so such curves cannot stomp element morphs — so this is harmless; but the code comment "The current fleet's takes are clean" (line 455) is false for this file.
- **Direction convention, proved end to end:** `SingleStickVesselTransformer.Pitch()` = `AngleAxis(−joystick.y·…, courseTransform.right)` → stick UP = nose UP; `Yaw()` = `AngleAxis(+joystick.x·…, up)` → stick RIGHT = nose RIGHT; `Roll()` = `AngleAxis(−joystick.x·…, forward)` = bank into the turn (lines 24–52). The driver sends `Pitch = −joy.y × 2` → stick up → `Pitch` negative → the **Up** clip at (0,−1) = nose-up pose; `Yaw = +joy.x × 2` → stick right → **Right** clip = nose-right pose with right-side-down bank. **The poses lean the whole hull INTO the turn the ship is making, same sense, 25–30° on top of the real rotation**, and the DampedTransform chains make the tendrils lag behind. Because `HeadRootBone` is the root of the entire skin, this is not a head nod: the BoxCollider (on the `SerpentMesh` node, not on a bone) stays put while the visible hull leans up to 30° away from it.
- **Root motion flag:** the prefab sets `m_ApplyRootMotion 1` and the `.meta` names `rootMotionBoneName: HeadRootBone` with rotation NOT baked into pose (`keepOriginalOrientation 0`). Whether Unity is applying the HeadRootBone rotation to the `SerpentExport` transform as root motion instead of to the bone cannot be measured offline; the visible result is the same lean either way, but a replacement that moves `HeadRootBone` off the skeleton root changes which object the "root motion" lands on. Keep `HeadRootBone` as the root bone.

### 3.4 Rest-pose handling
None in code (no `CaptureRestRotations`). The rest pose is the FBX bind pose, reproduced by the prefab's per-bone overrides (§2). The Animator writes `HeadRootBone` (and, via the unwired take, nothing else) every frame; the DampedTransforms then write all 23 tendril bones; the SkinnedMeshRenderer reads bones after the rig. No drift: the poses are absolute single keys.

### 3.5 Flare slots (`VesselAnimation.cs:527–548`)
`FlareEngine()` → `SkinnedMeshRenderer.materials[3].SetFloat("_ColorMultiplier", 5)`; `FlareBody()` → `materials[0]` ×5 (or `1 + amount×4`). **On this mesh slot 3 is the artist's `Domain` stripe submesh and slot 0 is `Body`** — so "flare engine" brightens the orange/jade trim, and "flare body" brightens the hull (which is also the slot the domain paint lands on, §5). The green engine pads (slot 1) are never flared. (Fleet intent per the `VesselCustomization` tooltip is slot order Domain/Body/Window/Engine; the Serpent exports Body/Engine/Window/Domain.)

---

## 4. ELEMENT MORPHS

### 4.1 Shipped shapes (probe lines 37–40; `bpy_measure.txt` SHAPE KEYS; diag 18.253 file units)

| shape | verts indexed / moved | maxD (fraction of diag) | sumD | where the travel is (by slot / by bone, % of sumD) | posed bounds change | status |
|---|---|---|---|---|---|---|
| `Space` | 1,797 / 1,646 (bpy 1,620 > 1e−4) | **2.145 (11.75 %)** | 1,455 | Domain 52 % / Body 48 %; HeadTail5.R 22.4, HeadTail5.L 22.4, BotTail5.R 21.7, BotTail5.L 21.7, ChassisBone 11.7 | x ±4.20 → **±5.21**, y min −10.10 → −10.52 | REAL — the four tip bulbs swell and spread outward ~1 unit per side; chassis plate shifts back 0.24 |
| `Mass` | 3,078 / 2,649 | **0.854 (4.68 %)** | 208 | Body 64 % / Domain 36 %; **BotTail5.R 22.2 vs BotTail5.L 7.9; HeadTail5.R 17.1 vs HeadTail5.L 6.4**; Chassis 11.9; HeadRootBone 8.7 (mean (0,−0.19,+0.22): head back/up) | ±0.06–0.15 everywhere | REAL but faint and **left/right asymmetric** (right tips travel 2.2–2.7× the left) |
| `Time` | 1,183 / 929 (bpy 805) | **2.634 (14.43 %)** | 531 | Body 55 % / **Engine 31.5 %** / Domain 13 %; **EngineBone 56.6 (mean (0,−0.65,0) = slides BACK)**, ChassisBone 43.4 (mean (0,+0.27,+0.07) = forward/up) | **y max 1.487 → 3.064**: the chassis plate's prow extends 1.58 AHEAD of the nose | REAL — a prow/spear forms ahead of the head while the engine recedes |
| `Charge` | 1,957 / 1,735 (bpy 1,040) | **3.048 (16.70 %)** | 857 | Body 85 % / Domain 15 %; ChassisBone 46.9 (mean (0,−0.46,+0.48) = back and UP), all four tips 10.9 each (mean (0,−0.94,0) = straight BACK), spikes 2.3–2.4 each (mean ≈ (±0.32,−1.25,0.06) = back and out), HeadTailSpikes 1.7 | y min −10.10 → **−12.38** (tendrils 2.28 longer) | REAL — tendrils lengthen aft, spikes grow, carapace lifts |

  `VesselElementalMorphConfigSO.TryResolveElement` (lines 60–90) binds these by exact tail match after the last `.` (then whole-token fallback), case-insensitive; `FullWeight` = last frame weight = **100** for all four (probe `fullW=100.0`). Feel: `Resources/VesselElementalMorphConfig.asset` `morphDuration 0.75`, `morphEase 4` (InOutSine), weight = level/10 over the [0,10] band. The prefab pins all four weights to 0 at rest (SMR overrides).

### 4.2 What each element should express on THIS hull (fleet rule: Space = reach/presence, Time = rate/mobility, Charge = threat/energy, Mass = size/volume; abilities from `element_ability_table.py Serpent`, `SERPENT_SNIPER_SCOPE.md`, `SERPENT_SEED_WALL.md`, `SERPENT_FUEL_PELLETS.md`)

| element | ability (input) | what grows with the element | proposed morph on the replacement (file units; target travel 3–15 % of an ~18-unit diagonal = 0.55–2.7) |
|---|---|---|---|
| **Charge** | **Sniper Shot** (RT while scoped): cooldown 12 s → 5.4 s at L10 (`SniperShotActionExecutor.cs:212`), L5 **Pierce** = the round breaks super-shielded mass (`:504`); the round goes "down the vessel's own nose" | the rifle: the thing at the NOSE | **Extrude a barrel/stinger forward from the visor's centreline by +2.0 (11 %)** (vessel +4 units ahead of z 2.97), thicken the head's dorsal spine ridge ×1.3, and keep the shipped spike growth (+0.8 on the four spikes). Drop the shipped "tendrils lengthen aft" — the weapon is forward, not behind. |
| **Mass** | **Seed Wall** (B): wall size and spacing ×1 → ×2 at L10 (`SeedAssemblerActionExecutor.cs:205`), L5 Lockdown; the stopped Serpent IS the seed | volume of the thing that becomes a wall seed: the thorax/carapace | **Inflate the chassis plate + engine housing radially ×1.25 about their own centre (max travel ≈ 1.0, 5.5 %)** and widen the head crown by 0.5 per side; **mirror-symmetric** (the shipped Mass is asymmetric and 0.85 max — invisible at 250 units). Optionally extrude two flank plates (wall "bricks") 0.8 outward. |
| **Space** | **Scope** (LT): FOV 22° → 11° at L10, floor 8° (`SniperScopeActionExecutor.cs:289`), L5 Deep Focus ×1.6; also the wall assembler's reach ×2 (`SerpentWallAssembler.cs:280`) | reach: the eye and the span | Keep the shipped idea and make it bigger: **spread the four tendrils outward +1.5 per side (8 %)** (x ±4.2 → ±5.7) and swell the tips 1.3×; **enlarge the visor (Window submesh) 20 %** — the scope is the eye. |
| **Time** | **Solid Fuel Pellets** (A): burn duration ×1.6 at L10 (`ConsumeBoostAction.asset` `timeDurationMultiplier`), a tank of four pellets that stack | rate/speed: sweep and exhaust | **Sweep the tendrils back and flatten toward the centreline (tips move ≈2.0 inward/aft, 11 %)**, **extend the three engine pads rearward 1.0 as exhaust bells**, keep the shipped prow (+1.0 ahead rather than +1.58, so Charge's barrel stays the longest forward element). |

  Magnitudes are chosen so each element owns a different silhouette axis: Charge = forward length, Mass = girth, Space = width, Time = aft sweep/flatness — all legible at the 56–84 px the hull occupies on screen (§5).

---

## 5. IDENTITY

- **What it is** (`Docs/claude/ABOUT_PROJECT.md:21`, `SERPENT_SNIPER_SCOPE.md:1–30`, `SERPENT_SEED_WALL.md:6–40`, `VesselStatus` prefab block `vesselType 7`, `_name Serpent`): the fleet's **one-thumb sniper** — `IsSingleStickControls` (desktop: `SingleStickMouseInputStrategy`), `SingleStickVesselTransformer` with a **stationary stance** (B stops the ship; `TurnScalar` ×3 while stopped, debuff-immune, the ship becomes a wall seed), **LT scope** (a second magnified picture in an eyepiece, the pilot's camera never moves), **RT contextual**: scoped = **sniper shot** (one hitscan down the nose, pierces by default, breaks armour at Charge 5), unscoped = **cloak** (leaves a super-shielded seed + a frozen illusion of the hull; the pilot sees their hull as a domain-coloured Fresnel outline via `CloakGhost`, drawn over a `BakeMesh` of the SkinnedMeshRenderer — `CloakSeedWallActionExecutor.cs:379–474`), **A = fuel pellets** (stacking speed bursts). Trail is laid **unshielded** (`VesselPrismController.shielded 0`, `SERPENT_SEED_WALL.md`); its walls grow from seeds (`SerpentWallAssembler`). Speed class: `MinimumSpeed 10`, `ThrottleScaler 50`, `PitchScaler 80`, `YawScaler 80`, `RollScaler 20`, `RotationThrottleScaler 0.4`, `MaxBoostMultiplier 5`, `boostMultiplier 4` (prefab) — same throttle scaler as the Rhino (50), below Squirrel 60 / Dolphin 68 / Urchin 65, far below Manta 180. AI: `SerpentAIBoostPolicy`, `AIPilot` abilities ConsumeBoost (8.1 s) + CloakSeedWall (4.3 s).
- **Camera**: `SerpentCameraSettingsSO` `followOffset (0, 0, −250)`, `dynamicMin/MaxDistance 250`, `farClipPlane 12000`, `nearClip 0.3`. At 250 units a 22.6-unit-tall hull subtends **7.8 % of the frame height at a 60° vertical FOV (≈ 56 px at 720p, 84 px at 1080p)**; it reads as a dark silhouette with one bright trim colour and four sticks. Detail finer than ~0.4 vessel units (0.2 file units) cannot read; the 499-vertex ringed tip bulbs are invisible at this distance. `VESSEL_TAIL_AND_JETS.md:444` says the mesh, collider and camera "disagree by an order of magnitude" — measured: hull 23.2 units long vs camera 250 (ratio 10.8:1; the Dolphin's is 20 : 5.3 = 3.8:1 per its dossier).
- **Silhouette as shipped** (`renders/serpent_rest_*.png`): a **hornet/mantis head** — a helmet-shaped head with a black wrap-around visor (Window) under an orange-trimmed crown, a short carapace plate (ChassisBone) and a squat engine box with three green pads facing aft; **four long whip tendrils** trailing back — the upper pair rising like antennae to +12 vessel units, the lower pair dropping to −9 — each with a short spike near its root and a ringed bulb tip. From the gameplay camera (behind and slightly above) it reads as a small head and a splayed X of sticks. The FBX's Domain material is authored yellow with a red emissive (renders salmon); in Unity it wears `GreenAccentVesselMaterial` (jade).
- **Domain colour slot — measured, and it contradicts the fleet convention**: `VesselCustomization` on the Serpent serializes only `_shipGeometries [SerpentMesh]`; `_domainReplacesMaterials` is unserialized (empty) and `_domainMaterialSlot` unserialized (default 1), so `ShipHelper.ApplyShipMaterial` takes the SkinnedMeshRenderer branch and writes the domain material into **`materials[0]` = the artist's `Body` submesh (64 % of surface area)** (`VesselHelper.cs:164–169`). The artist's `Domain` stripes (slot 3) stay jade for every domain. The tooltip at `VesselCustomization.cs:22–32` says Body must never be painted and the list "normally names exactly GreenAccentVesselMaterial". Either author `_domainReplacesMaterials = [GreenAccentVesselMaterial]` on the prefab, or export the replacement with `Domain` in slot 0 (§8). The green **Engine** pads: no `.meta` remap exists for `Engine`; with `materialLocation 1` (external, legacy) Unity resolves it to a project material by name or generates one — `Assets/_Models/Vessel Models/Engine_2.mat` (URP Lit, `_BaseColor (0, 0.02, 0.906, a 0)`, referenced by nothing) sits beside the FBX and is the likely product; what the pads wear in play is not pinned by any asset.
- Card art / codex: no Serpent-specific card background, codex entry or icon doc was found under `Docs/` (grep `serpent` + `codex|card art|icon`); the HUD binds `*-PLACEHOLDER.png` silhouettes for Charge/Space (`VESSEL_HUD_AND_ELEMENTS.md:199`).

---

## 6. GAMEPLAY MOUNTS (vessel frame, Unity units, scale-2 instance; the §1 ×1.5 override not applied)

| mount | node / component | position / size | source |
|---|---|---|---|
| **hull collider** | `BoxCollider` 2203058038973014175 on `SerpentMesh` GO (layer 8), `m_IsTrigger 1`, `m_Size (4.5, 3.28, 2.96)`, `m_Center 0` in the mesh node's frame (= model-root frame, scale 2) → **vessel (9.0 wide, 6.56 tall, 5.92 long) centred on the origin**: covers the head's front from z −2.96 to +2.96 (the head itself spans z −5.0…+3.0, the body −10.9…+3.0; tendrils entirely outside). `ImpactCollider` 8026890533229392904 on the same GO → `impactorObject = VesselImpactor` 3477502592268722062 (`SerpentImpactorDataContainer.asset`, `networkVesselImpactor`). Not on a bone: the Animator's 30° leans move the hull away from it. | prefab added components |
| **tail** | `VesselTail.prefab` instance `&3901220300006002`, `(0, 0, −262.5)` (= −1.05 × 250), `widthScale 12.5`; `VesselTailAndJets._trails []` → it collects `VesselTail`/`VesselJet` by `GetComponentsInChildren` (`VesselTailAndJets.cs:169–175`) | `mods.txt`; `VESSEL_TAIL_AND_JETS.md` table |
| **jets** | **none** (no `VesselJet` instance; §5 of the jets doc: the EngineBone "cannot be resolved from the asset"). Measured engine for a future jet: `EngineBone` head **(0, 0, −5.39)**, the three green pads centroid **(0, 0.21, −8.70)** spanning x ±3.36, y [−1.41, 1.83], rear face z −9.30 (`bpy_measure.txt` slot 1 / EngineBone cluster). A jet mounted by name on `EngineBone` at local (0, 0, −3.9) lands on the pad face and would ride the (unwired) SpeedIncrease slide (−2.18). | measured |
| **skimmer** | `VacuumSkimmer` GO (child of `OrientationHandle`), `localScale (50,50,50)`, `SphereCollider r 0.5` → **radius 25 at the origin**, **`m_Enabled 0`**; `Skimmer` `vaccumAmount 50`, `vacuumCrystal 1`, `visible 0`, `markerDistance 70`; `VesselStatus._nearFieldSkimmer` → it, `_farFieldSkimmer` null. MeshFilter = built-in sphere 10207. | prefab blocks |
| **guns / bays** | none as nodes. The sniper line is `transform.forward` from the scope eye, which `ScopePipView` places at **1.05 × `PrismOcclusionCorridor.MeasureCircumscribedRadius`** = the farthest extent of the SkinnedMeshRenderer's `localBounds` from the origin (`PrismOcclusionCorridor.cs:219–225`): with tendrils to z −20.2 that radius is ≈ 20.5, so **the eye sits ≈ 21.5 units ahead of the origin**. A replacement with shorter tendrils moves the eye closer; a longer one pushes it out. | `SERPENT_SNIPER_SCOPE.md` "The eye is MEASURED" |
| **PIP / rear camera** | `RearViewCamera` (`PipCamera.prefab`) under `OrientationHandle` at `(0, 0, 25)` rot 180° about Y, `m_IsActive 0`; `Pip.pipCamera` → its Camera stub 1507913478050007905 | `mods.txt` block 4 |
| **FX / illusion** | the cloak's illusion and reveal glow are runtime `BakeMesh` copies parented to `shipRenderer.transform` (`SerpentMesh`), so they follow whatever the mesh node is; no authored FX mount nodes. | `CloakSeedWallActionExecutor.cs:379–474` |
| **Rigidbody** | on the root: kinematic, mass 1, no gravity. | prefab |

---

## 7. IN-FLIGHT WORK (`git fetch origin` + `python3 Tools/Build/who_works_where.py query …`, 2026-10-17)

- **In flight:** `cece/vibrant-archimedes-n3xn3x` (2026-10-16, "feat(serpent-charge): a sniper duel - only snipes on rival pilots score", 7 commits incl. "fix(serpent): a cloak hides the whole vessel from observers, tail included", "fix(ai): AI Serpent cloaks replicate"). `git diff origin/bleeding-edge...origin/cece/vibrant-archimedes-n3xn3x --stat -- Assets/_Prefabs/Spacevessels/Serpent.prefab` → **2 insertions**: it adds `ReplicateThroughInput: 0 / 1` to the two `AIPilot.abilities` entries (prefab lines 2069–2077). **No model, rig, material, animator or collider lines** — a model swap does not collide with it textually; merge order is free.
- **Landed on bleeding-edge:** #1067 `claude/serene-edison-lfv24f` (2026-10-13, Shombith), #1051 `claude/serpent-mass-seed-wall` (2026-10-13, Garrett). `SerpentExport.fbx`, its `.meta`, the controller and the prefab are identical between `HEAD` and `origin/bleeding-edge` (`git diff --stat` empty).
- **Talk to:** Shombith (1), Garrett (1) — Garrett designed the Seed Wall and tuned the Serpent in Unity on 2026-10-09/10/13; the Serpent's abilities and the AI are theirs. The model itself has no in-flight owner.

---

## 8. CONSTRAINTS FOR A REPLACEMENT (numbered, exact)

1. **File**: write **in place** over `Assets/_Models/Vessel Models/SerpentExport.fbx` (keep guid `7fad38a2fd9fce64088bb244e1473ffe`; keep the `.meta`, adding one `externalObjects` entry for `Engine`). Do not touch `serpent.fbx` (inactive vestige; retire it separately).
2. **Node names and paths to keep** (29 stubs + 6 clips bind by path hash): `SerpentArmature` ▸ `HeadRootBone` ▸ {`EngineBone`, `ChassisBone`, `HeadTail1.R`▸`HeadTail2.R`▸`HeadTail3.R`▸`HeadTail4.R`▸`HeadTail5.R`, `HeadTail1.R`▸`HeadTailSpike.R`, the `.L` mirror, `BotTail1.R`▸`BotTail2.R`▸{`BotTailSpike.R`, `BotTail3.R`▸`BotTail4.R`▸`BotTail5.R`}, the `.L` mirror}; and the mesh node **`SerpentMesh`** (hosts SMR + collider + ImpactCollider + `_shipGeometries`). `HeadRootBone` stays the single skeleton root (root-motion bone). Geometry name free; keep ONE mesh, ONE SkinnedMeshRenderer.
3. **Bone rest TRS**: keep each bone's rest `Lcl Translation/Rotation` as in §1.2 **or** delete the 24 transforms' `m_LocalPosition/m_LocalRotation` overrides from the prefab's PrefabInstance block (`mods.txt` lists them) — otherwise the prefab's overrides win over the new rest pose and the skin tears. Resolve the `m_LocalScale 1.5` override on `−2193820272513555211` in the editor first (§1) and remove it if it is unintended.
4. **Takes to keep** (same names, one key each, blend-shape curves optional but harmless): `SerpentArmature|SerpentUpAnim` (HeadRootBone +30° about bone Y = nose up), `SerpentArmature|SerpentDownAnim` (−30° = nose down), `SerpentArmature|SerpentLeftAnim` (yaw 25° left + 25° bank left-down), `SerpentArmature|SerpentRightAnim` (mirror), `SerpentArmature|SerpentIdleAnim` (rest), `SerpentArmature|SerpentSpeedIncreaseAnim` (Chassis −3.566 / Engine −1.090 aft; unwired, keep for the controller's future). Take = armature name + `|` + action name, so the armature object must stay `SerpentArmature`.
5. **Blend shapes**: exactly `Space`, `Mass`, `Time`, `Charge` on the one mesh, full weight 100, each with travel 3–15 % of the diagonal and **mirror-symmetric** (§4.2 proposals), normals exported.
6. **Material slots**: export **`Domain` (0), `Body` (1), `Window` (2), `Engine` (3)** — the Squirrel/fleet order — so `ApplyShipMaterial` paints the trim and `FlareEngine` hits the engine; keep the names so the `.meta` remap (`Body`→BlueBase, `Domain`→GreenAccent, `Window`→Screen) binds, and add `Engine`→ an authored vessel material. If the shipped order must be kept instead, author `_domainReplacesMaterials = [GreenAccentVesselMaterial]` on `Serpent.prefab`.
7. **Export settings**: `UnitScaleFactor 100` (1 unit = 1 m, file scale 1.0), Z-up / −Y-front file axes (Blender native), root nodes at identity, **nose = +Y_b, up = +Z_b, pilot's right = +X_b**, `.R` on the pilot's right. The prefab's 180° Y + scale 2 then puts the nose on vessel +Z.
8. **Hull bounds to match** (file units, so the collider at `(4.5, 3.28, 2.96)` mesh-local, the skimmer, the 250 camera, the −262.5 tail and the scope-eye radius stay right): head within x ±1.7, y_b `[−2.5, +1.5]`, z_b `[−1.7, +2.0]` (the visor at y_b +1.5 = the nose); total extent ≈ 8.4 × 11.6 × 11.3 (x, y, z) with the tendril tips at y_b ≈ −9…−10. Keep the origin inside the head at the same place (the HeadRootBone head 2.07 behind it). If the tendrils are shortened, the scope eye moves in proportionally (§6).
9. **Forward axis**: vessel +Z = file +Y (§1.1). Window faces +Y_b; engine pads face −Y_b.
10. **Wiring path**: **in-place overwrite** (same guid, same paths, same take names → zero prefab edits beyond the optional override cleanup in 3 and the material fix in 6). Risk: moderate — the 24 rest-pose overrides, the unknown ×1.5 node, and the rig's two mis-wires are inherited verbatim; validate with `assimp info` before/after (submesh order, 6 animations, 4 shapes), `fbx_probe.py --takes`, and then the editor's **FrogletTools ▸ Vessels ▸ Audits ▸ Audit Vessel Elemental Morphs** + a play test of the four poses. A **new-file + editor swap** would require re-pointing all 29 stubs (bone fileIDs not derivable offline) through a VesselRigSwapper-style tool — only worth it if the skeleton must change.

---

## 9. QUALITY BAR

**Keep (good in the shipped model):** a clear, unique identity (hornet/mantis head + four whip tendrils, nothing else in the fleet looks like it); a real two-tone read (dark hull + one trim colour + black visor + green pads) that survives 56–84 px; a complete, cleanly named 27-bone rig with 5-link mirrored chains and spike bones, 100 % skinned, all-quad topology, one mesh/one renderer, UVs in 0..1; four REAL element shapes with sensible intent (Space spreads the reach, Time forms a prow, Charge grows spikes); six well-named single-key poses that the Animator blends and that read correctly in sign; the DampedTransform lag rig that makes the tendrils trail.

**Weak (fix in the replacement):** (1) **46 % of vertices are in four tip bulbs** that are sub-pixel at 250 units while the head (632 verts) is the only part the camera can read; (2) the hull is **as tall as it is long** (22.6 × 23.2) with a **small head (8 units) and 20-unit sticks**, so from the gameplay camera it is a sparse X with no mass — a sniper should read as a body with a long BARREL forward, not antennae back; (3) **slot order inverts the code's expectations** (domain paint on Body, engine flare on the trim, engine pads unflared and unpinned); (4) `Mass` is invisible (0.85) and asymmetric; `Charge` grows the wrong end; (5) the rig ships with a dead `HeadRig` constraint and a duplicated `BotTailRig5L` (prefab); `SpeedIncreaseAnim`, `Throttle`, `Roll`, `Boost` are dead content; (6) the stern (SpeedIncrease, Time) and the engine pads are modelled at 144 polys — fine — but the Engine material is not authored anywhere; (7) 24 baked rest-pose overrides and one unexplained ×1.5 make the prefab brittle to any re-export.

**Measured polycount:** 4,286 quads / **8,572 tris / 4,372 verts**. **Target for the replacement at the 250-unit camera:** **6,000–10,000 tris, ≤ 4,500 verts**, allocated ≥ 50 % to the head/visor/thorax (the only part that resolves), ≤ 25 % to all four tendrils (≤ 60 verts per tip), the rest to the engine/stern; silhouette-first (every element morph must change the outline, §4.2), one dark hull material, one trim submesh ≥ 25 % of area so the domain colour reads, the visor ≥ 6 % so the "eye" reads, and engine pads that a jet can be mounted on by name (`EngineBone`).
