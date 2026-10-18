# MODEL DOSSIER — Urchin

> **RIG SWAP, 2026-10-18 — the prefab now DRAWS the rig.** `Urchin.prefab` nests
> `urchan_shapekey_with_animations.fbx` (guid `7f9bbc7b…`) under `OrientationHandle` at localScale **0.474905**
> (`VesselRigSwapper`'s measured fit; the instance is named `urchan_shapekey_with_animations`, so that tool reports
> "already swapped"). The 13 MeshFilter + MeshRenderer pairs that drew `Urchan_Test.fbx` are removed; the placeholder
> FBX stays in the repo, drawn by nothing. Rebuilt by `python3 Tools/Build/vessel_model/build.py urchin`
> (`Tools/Build/vessel_model/specs/urchin.py`, the artist-hull path), in place, same guid. Sheet:
> `sheets/urchin_rig_swap.jpg` (row 1 the free-flight rest; row 2 the four morphs at 100% from the chase view; row 3
> the artist's pitchup / yawright / rollright / ball takes).
>
> **What the file is now.** The artist's 6,080-vert / 12,090-tri mesh, 38 bones, four slots (`BASE, accent,
> windsheild, N`) and eleven takes, kept. Its REST is the artist's free-flight pose: the first key of `urchan_fly`
> (bind + the six `winggrowrot*` pod holds at 1.75), which nine of the eleven takes start from and which equals the
> placeholder prefab's free-flight layout part by part to < 0.5 mm (×0.474905). The rig's node chain had parked the
> last frame of `urchan_yawright` (root −9.9°, fuse −14.1°, holds 10.5°, shrouds 19–41°, one-sided), a twisted pose
> the game never drew, so it is NOT the rest (LESSONS 37). The spec gates against `rested_reference()`: the artist
> file with only its animated nodes' Lcl transforms set to that key (codec surgery; mesh, skin, bind, takes and
> materials are the artist's bytes). Four REAL morphs, lowercase as the file had them: **mass** 4.4% of the
> diagonal (the ball ×1.23 about its centre, shells +0.025), **charge** 9.3% (barrels ×1.8 forward and ×1.3 thick,
> the gear rings ×1.3 with their teeth thrown out), **space** 6.0% (all six pods 0.14 out along their radials),
> **time** 10.9% (the four engine pods swept 12° back about the ball, the jet domes ×1.2 aft). Gates: take parity
> 127 moving part-takes at 0.000 / 0.0°, moving-node and material parity clean, `rest_compare` vs the rested
> reference 0.000 / 0.00° on the Blender reader (the assimp reader shows the 1.75 node scale written as 1.0 under
> the six holds, positions equal to 0.0012 cm: Blender cannot hold a rest scale, so it is baked into the geometry).
>
> **The contract in the prefab.** `UrchinAnimation` still drives the placeholder's part transforms exactly as before
> (the 12 box colliders, the four `VesselJet`s and `MuzzleL/R` live on them); in `LateUpdate` the rig's `fuse` and
> six `winggrowrot*` holds FOLLOW `Body`, `LeftGun/RightGun`, `JetTop*/JetBottom*` (rest-relative rotation, position
> and the 1.75 ↔ 1.0 tuck scale), so every collider keeps enclosing its drawn pod. `.l` = the pilot's left = Unity −X
> = the `Left` parts. The SkinnedMeshRenderer is found by `GetComponentInChildren` (`updateWhenOffscreen`), its four
> element shapes reach the screen through `VesselAnimation` by name, and its engine/body flares go through
> per-material-index property blocks (slot 3 `N`, slot 0 `BASE`). The `.meta` remaps `BASE / accent / windsheild / N`
> to `BlueBase / GreenAccent / Screen / EngineVesselMaterial` and sets `isReadable 1` (the hull fusion reads it);
> `_shipGeometries` gains the instance root, which `VesselCustomization.ResolveModelGeometries` turns into the
> skinned renderer, so the domain paints the `accent` slot. The drawn jet mouths sit within 2 mm of the authored
> `VesselJet` points (no jet moved). **Still stale:** the four `Urchin_*_HullFusionBake` assets key the placeholder's
> Body mesh; the runtime warns once and solves on a worker until **FrogletTools > Vessels > Wiring > Bake Crystal
> Hull Fusions** is re-run in the editor. Morphs are not re-fitted to the colliders: at 100% the ball outgrows its
> sphere collider by ~0.049 u, Space carries pods ~0.066 u out of their boxes, Charge's barrel tip ~0.103 u forward,
> Time's pods ~0.12 u aft.
>
> Sections 1-8 below are the pre-swap record and stay the explanation of why each number is what it is.

> **Record of the ARTIST file as shipped before 2026-10-17.** On 2026-10-17 the file this dossier measures was
> rebuilt in place by `python3 Tools/Build/vessel_model/build.py urchin` (spec `Tools/Build/vessel_model/specs/urchin.py`,
> skill `/vessel-model`): same path and guid, same node, geometry, bone and material names, every bone's rest pose
> reproduced to 0.000 (`rest_compare.py`). Sections 1-7 describe the artist file the spec was written against and
> stay the explanation of WHY the spec keeps each name and number; section 8 is the contract the rebuilt file meets.
> Paths under `<scratchpad>/` were the measuring session's temporary files: the commands beside them regenerate
> every number from the repo.


Measured 2026-10-17 on branch `ccr-d93a6dfb-ugbxng` at `b7f0fbbf3` == `origin/bleeding-edge` `b7f0fbbf3` (`git diff origin/bleeding-edge --stat` over the prefab, both FBXs, the `.meta`, `UrchinAnimation.cs`, `VesselAnimation.cs` and the camera SO is empty). Every number names where it was read. Where a doc and an asset disagree the asset is the record and the disagreement is called out (§1.1, §2, §5, §6.5).

Sources:
- `P` = `Assets/_Prefabs/Spacevessels/Urchin.prefab` (5,046 lines), `P:NNNN` = line; `TREE` = `python3 .../scratchpad/tools/prefab_tree.py P` saved at `<scratchpad>/urchin_prefab_tree.txt` (819 lines)
- `F` = `Assets/_Models/Vessel Models/Urchan_Test.fbx` (0.4 MB, FBX 7400), guid `fee259a2cc152ca409c9c85a43ecc91e`; `M` = its `.meta`
- `R` = `Assets/_Models/Vessel Models/urchan_shapekey_with_animations.fbx` (2.6 MB, FBX 7400), guid `7f9bbc7b4abd6234e9e673dc70270f07` — the UNWIRED rig
- `PROBE` = `python3 Tools/Build/vessel_model/fbx_probe.py "<file>" --takes`, saved at `.../scratchpad/probe/Urchan_Test.txt` and `.../probe/urchan_shapekey_with_animations.txt`; `PARTMAP` = `.../scratchpad/partmap/<same>.txt` (bpy world-space bounds); `MEASURE` = `python3 Tools/Build/measure_vessel_models.py --no-refs --only Urch`
- `GEOM` = bpy 5.2 per-part analysis `<scratchpad>/urchin/geom.py` → `.../scratchpad/urchin/geom.json` (per-slot polygon counts, centroids, manifoldness, UVs, the 31 ShootPoints)
- `RENDER` = Cycles renders of the SHIPPED composite (prefab scales applied) by `.../scratchpad/urchin/shipped_render.py`: `.../scratchpad/urchin/shipped_1.75_{nose34,tail34,side,top,nose,chasecam}.png` (free flight) and `shipped_1.0_*.png` (riding / as the FBX is authored); older single-file renders `.../scratchpad/renders/Urchan_Test_cy.png`, `.../renders/urchan_shapekey_with_animations_cy.png`
- `A` = `Assets/_Scripts/Controller/Animation/UrchinAnimation.cs` (170 lines), `VA` = `.../VesselAnimation.cs`, `GVT` = `Assets/_Scripts/Controller/Vessel/GunVesselTransformer.cs`, `VT` = `.../VesselTransformer.cs`
- `CAM` = `Assets/_SO_Assets/Camera/UrchinCameraSettingsSO.asset`; `C` = `Assets/_Models/Animations/MantaAnimatorController.controller` (guid `f922b4ddeaa211f498ff3a539e6ce230`)

Frames: **file** = the FBX's own frame (`PROBE` line 2: `up=+1 front=+2 coord=+0` = UpAxis +Y, FrontAxis +Z, CoordAxis +X; units cm). **blender** = bpy after import = file rotated +90° about X (`PARTMAP`: every node `rot=(90,0,0) scale=0.01`): blender (x,y,z) = (file x, −file z, file y). **Unity** = the vessel frame (+Z nose, +Y up, +X pilot's right). Derived in §1.4: Unity (x,y,z) = (−file x, file y, file z) × 0.01 × node scale.

---

## 1. LIVE MODEL

### 1.1 What the prefab draws and how
- **No nested model PrefabInstance.** `F` is drawn by **13 direct `MeshFilter.m_Mesh` references** into guid `fee259a2…` (`P:76, 433, 577, 914, 1119, 1293, 1403, 1575, 1687, 1862, 2020` + two more; `TREE`), one per prefab-native GameObject. Every `m_CorrespondingSourceObject`/stripped stub in `P` belongs to the FIVE other nested prefabs (VesselJet ×4, VesselTail, PipCamera, Skimmer, UrchinHUDVariant) — **none points into `F`**. There are 0 `SkinnedMeshRenderer`s, 13 `MeshFilter`s (`Docs/VESSEL_CONSTRUCTION.md:43` agrees: "Urchin | 13 | 0 | Urchan_Test ×13 | 0").
- **Doc vs asset:** `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md:55` says the Urchin/Rhino "rigs were swapped for the PUPPETRY with the morph honestly absent". **The asset says otherwise**: `P` has no reference to `R` (`grep -rl 7f9bbc7b… Assets` → only `R.meta` and four `.controller`s). The Urchin is NOT rig-swapped; `R` is wired to nothing but orphan blend trees (§3.4). `VesselRigSwapper.cs:189-215` still carries the swap table (`InstanceScale 0.474905`) as a tool nobody has run on this prefab.
- Host chain (`TREE`): root `Urchin` (`&6417075533431866457`, tag `Player_Ship`, 24 components) → `OrientationHandle` (`&1878660672777590133`, identity) → **`Body`** (`&7952856352645082721`, layer 8 `Ships`, identity TRS) → the six appendage nodes, each with its shroud as a CHILD:
  ```
  Body [MeshFilter 2959467708922500008, MeshRenderer, SphereCollider, ImpactCollider]
    GunContainer [Gun]  + 30 bare "Sphere (n)" transforms (scale 0.01) — see §6.4
    JetBottomLeft  (scale 1.75) [MeshFilter 3301801484668628847, BoxCollider, ImpactCollider]  > ShroudBottomLeft  (1) [MeshFilter 1173804494966244414]  + VesselJet (JetBottomLeft)
    JetBottomRight (scale 1.75) [MeshFilter 3651206300688657872 ...]                           > ShroudBottomRight (1) [MeshFilter -1309588489851300069] + VesselJet (JetBottomRight)
    LeftGun        (scale 1.75) [MeshFilter 9134634040662503695, Gun, BoxCollider ...]         > ShroudLeft  (1) [MeshFilter -3318364715813557357], MuzzleL (-3,0,4)
    RightGun       (scale 1.75) [MeshFilter 8514049938098572669, Gun ...]                      > ShroudRight (1) [MeshFilter -8852780382351287992], MuzzleR (3,0,4)
    JetTopLeft     (scale 1.75) [MeshFilter 4213887719459583113 ...]                           > ShroudTopLeft  (1) [MeshFilter 8436260602439234206]  + VesselJet (JetTopLeft)
    JetTopRight    (scale 1.75) [MeshFilter -4054977461692037399 ...]                          > ShroudTopRight (1) [MeshFilter -6511944097885156458] + VesselJet (JetTopRight)
  ```
  (`TREE` lines 270-560; `P:3,34,532,872,1075,1249,1361,1533,1643,1817,1976` for the GameObjects.)
- Other users of guid `fee259a2…` (`grep -rl … Assets`): `Assets/_SO_Assets/CrystalHullFusion/Urchin_{Mass,Charge,Time,Space}_HullFusionBake.asset`, each `hullMesh: {fileID: 2959467708922500008}` (= the `Body` mesh), `hullVertexCount: 11285`, `hullPartCount: 13`, `hullHash: 35758063` (`Urchin_Mass_HullFusionBake.asset:17-21`). Nothing else.

### 1.2 Instance transform — there is no single instance root; every node is placed by the prefab
- `Body` and every appendage node: `m_LocalPosition (0,0,0)`, `m_LocalRotation` identity (`TREE`). **Twelve of the thirteen part nodes are not at scale 1**: the four jets and the two guns author `m_LocalScale (1.75, 1.75, 1.75)` (`TREE` JetBottomLeft/JetBottomRight/LeftGun/RightGun/JetTopLeft/JetTopRight), and the six shrouds are their children at scale 1, so they inherit 1.75. Because all 14 FBX meshes share the model origin (`PROBE`: every node `T=(0,0,0)`), **the 1.75 scales the appendages about the hull centre — they are drawn 1.75× larger AND 1.75× further out than the FBX authored them**. `A:77-89` then animates exactly this: the four jet nodes lerp to `localScale 1.75` in free flight and to `1.0` while `VesselStatus.IsAttached` (riding), so the FBX pose IS the balled "riding" pose and the prefab's authored 1.75 is the extended free-flight pose. The guns stay at 1.75 always (not in `scaledParts`).
- `Docs/VESSEL_CONSTRUCTION.md:274` quotes "Urchin x/y/z span 0.432/0.406/0.414" — that is the FBX at node scale 1 (the balled pose). The resting silhouette the pilot sees is §1.3's 0.756 × 0.666 × 0.455.

### 1.3 Import scale → hull size in Unity units
- `M`: `globalScale 1`, `useFileScale 1`, `useFileUnits 1`, `bakeAxisConversion 0`, `meshCompression 0`, `weldVertices 1`, `isReadable 1` (the hull-fusion bake reads it), `importBlendShapes 1`, `animationType 2`, `fileIdsGeneration 2`, `clipAnimations []`, `externalObjects {}` (no material remap — the prefab's renderers wear project materials directly, §1.6).
- `PROBE` line 2: **`UnitScaleFactor=1.0`** → file units are centimetres; Unity file scale = 1.0 × 0.01 = **0.01** (the task note's "100 = cm" is inverted; it does not matter here because this file says 1.0, and the Squirrel/Dolphin dossiers measured the same rule). **Measured confirmation**: the `SphereCollider` authored in Unity on `Body` is `m_Radius 0.2072551` (`P`, `TREE`) = the FBX Body half-extent 20.3 cm × 0.01 (+2%); every appendage `BoxCollider.m_Size` equals its mesh's FBX extent × 0.01 to four digits (e.g. `JetTopLeft` `(0.08648413, 0.086484134, 0.15736264)` vs `UpperJet` extent `(8.648, 8.648, 15.736)` cm, `MEASURE`).
- Bounds, measured by rebuilding the prefab's scales in bpy (`RENDER` script output, Unity frame):
  | pose | min (x,y,z) | max | size W×H×L | diagonal |
  |---|---|---|---|---|
  | **riding / FBX as authored** (appendage nodes at 1.0) | (−0.216, −0.203, −0.208) | (0.216, 0.203, 0.207) | **0.432 × 0.406 × 0.415** | **0.723** |
  | **free flight / prefab rest** (appendages at 1.75) | (−0.378, −0.313, −0.249) | (0.378, 0.353, 0.207) | **0.756 × 0.666 × 0.455** | **1.106** |
  The body sphere alone: r 0.203, z −0.208 .. +0.2065 (`PARTMAP` Body). Nose tip z = +0.2065 (the window), tail-most point z = −0.249 (the top shrouds at 1.75).

### 1.4 Which axis is the nose
Geometry (`GEOM`, file cm): the Body's slot 2 window material (`Material.002`, 33 polys) has centroid **(0.00, 0.00, +17.96)** on a ±20.3 sphere — the canopy is the file **+Z** cap (`RENDER shipped_1.0_nose34.png`: three blue discs, one large, two small, form the "eye"). Body slot 0 centroid z +1.98, slot 1 (accent) z −0.83, so the circuitry leans aft. The gun barrels (`SideShooters`) are 7.4 cm long along z and their accent ring sits at z +0.84 with the body at +0.73 — barrels point +Z; `MuzzleL/R` are authored at local z **+4** under the guns (`TREE`). File +Z is also the FBX `FrontAxis`.
Unity: `VT:921-942` flies `transform.forward`; `CAM.followOffset (0, 0.83, −6.67)` sits behind; `VesselTail` at `(0, 0, −7.0)` (`P:4330-4360`, `TREE`); the four plumes are placed at each jet collider's **−z** face (`VESSEL_TAIL_AND_JETS.md:266-268`: "z pulled to the collider's rear face"; `VesselJet (JetTopLeft)` local z −0.052 = collider centre 0.0257 − half-size 0.0787 ✓). So **file +Z = Unity +Z = nose**; file +Y (UpAxis) = Unity +Y: every collider centre matches its mesh centre in y and z with NO flip (`JetTopLeft` c (−0.1052, 0.1307, 0.0257) ↔ `UpperJet` centre (10.53, 13.07, 2.56) cm). X: Unity imports this exporter's Y-up cm files as a conjugation by `0.01·diag(−1,1,1)` (proved on the Dolphin rig with fitted bone matrices, `Docs/VESSEL_CONSTRUCTION.md:737-745`; same Blender export convention here), so **Unity x = −file x**. Consequence for a mirror-symmetric model: a "Left" prefab node (collider centre x < 0) draws the mesh whose FILE centre is +x — i.e. `JetTopLeft` ↔ `UpperJet` (file +10.53), `JetTopRight` ↔ `UpperJet.003` (file −10.53), and likewise `Lower*`, `SideShooters*`, `*Hold*`. **This L/R pairing is the one claim not provable offline** (the two meshes of each pair are exact mirrors, `GEOM`; sub-asset ids are name hashes, §2) — confirm by reading `JetTopLeft`'s MeshFilter mesh name in the editor. The ShootPoints cloud (31 verts, `GEOM`) is itself x-symmetric and cannot settle it.

### 1.5 Full node hierarchy (`PROBE`, 14 models, all siblings at identity TRS, all geometry in one frame)
| node | geometry | verts | polys (tris) | slots ByPolygon {0:base, 1:accent, 2:window} | bounds file cm (x,y,z) | centre |
|---|---|---|---|---|---|---|
| `Body` | `Sphere.009` | 1,762 | 1,350 (3,460) | {980, 337, 33} | (−20.3,−20.3,−20.81)..(20.3,20.3,20.65) | (0,0,−0.08) |
| `SideShooterHold` | `Sphere.023` | 426 | 400 (848) | {323, 77} | (18.31,−7.27,−7.31)..(21.61,7.23,7.31) | (19.96,0,0) |
| `UpperJetHold` | `Sphere.033` | 316 | 324 (628) | {270, 54} | (6.74,10.57,−14.21)..(17.56,20.15,11.37) | (12.15,15.36,−1.42) |
| `LowerJetHold` | `Sphere.034` | 275 | 281 (546) | {223, 58} | (10.39,−17.89,−11.19)..(17.79,−10.84,8.11) | (14.09,−14.37,−1.54) |
| `UpperJet` | `Sphere.038` | 384 | 370 (764) | {256, 114} | (6.20,8.74,−5.30)..(14.85,17.39,10.43) | (10.53,13.07,2.56) |
| `LowerJet` | `Sphere.039` | 384 | 370 (764) | {256, 114} | (9.72,−15.98,−4.24)..(15.74,−9.97,6.71) | (12.73,−12.98,1.23) |
| `SideShooters` | `Sphere.040` | 318 | 276 (632) | {194, 82} | (13.81,−2.74,−2.14)..(19.41,2.52,5.23) | (16.61,−0.11,1.55) |
| `ShootPoints` | `Sphere.041` | 31 | 0 | — | (−17.32,−19.84,−13.32)..(17.32,20.3,13.58) | point cloud, unreferenced (§6.4) |
| `SideShooterHold.003`, `UpperJetHold.003`, `UpperJet.003`, `LowerJetHold.003`, `LowerJet.003`, `SideShooters.003` | `Sphere.008/.010/.011/.013/.014/.015` | same | same | same | x negated | mirror twins |
No armature, no bones, no skin, no blend shapes, no takes (`PROBE`: `bones (0)`, `takes (0)`; `MEASURE`: `blend shapes: none`).

### 1.6 Polycount, materials, UVs, topology (`GEOM`, `PROBE`)
- **Totals (13 drawn meshes): 5,968 verts, 5,392 polygons = 4,787 quads + 253 triangles + 352 n-gons, 11,824 triangles.** Unity's imported vertex count after seam splitting across the 13 parts is **11,285** (`Urchin_*_HullFusionBake.asset: hullVertexCount`). Body alone: 1,762 v / 1,350 f (226 n-gons) / 3,460 tris. Per side: hold+jet+hold+jet+gun+ring = 2,021 polys / 4,182 tris.
- **Topology defects (`GEOM`)**: `Body` has **102 boundary edges and 125 non-manifold edges** (the window recess is cut open; `open_end_centroid (0.57, −1.03, 11.91)` cm sits just behind the canopy). Every appendage is a closed manifold (0 boundary, 0 non-manifold) — the jets are **closed domes with no nozzle opening** (`UpperJet` 2 n-gons, 0 boundary edges).
- **Material slots** (same order on every part; `P` MeshRenderers, `TREE`): slot **0** `Material.004` (FBX Phong diffuse (0, 0.8, 0.145)) → prefab `BlueBaseVesselMaterial` (guid `54e78ce1…`) = base hull, **4,024 polys (74.6%)**; slot **1** `Material.009` (diffuse black, emissive (0, 1, 0.009)) → `GreenAccentVesselMaterial` (`539a8c65…`) = **the DOMAIN slot, 1,335 polys (24.8%)**, the emissive circuit/cross/ring panels; slot **2** (Body only) `Material.002` (diffuse (0.056, 0.058, 0.8)) → `ScreenVesselMaterial` (`923d3665…`) = the three canopy discs, **33 polys (0.6%)**. Areas on the Body (`GEOM`, cm²): base 4,804, accent 2,300, window 760 — the accent is 29% of the body surface. No slot 3 anywhere (§3.6).
- Which polys the domain paints: `Body` 337, `UpperJet` 114 ×2, `LowerJet` 114 ×2, `SideShooters` 82 ×2, `SideShooterHold` 77 ×2, `LowerJetHold` 58 ×2, `UpperJetHold` 54 ×2.
- **UVs**: one layer per mesh, u,v ∈ [0,1] except `UpperJetHold(.003)` (u max 1.05, 8 loops outside); `SideShooterHold` packs into u ≤ 0.178, v ≤ 0.107 (a 1.8% × 10.7% corner — flat materials today, but a textured replacement would need a repack).
- The three project materials all use the same shader (`0236447d…`), `_BaseColor (1,1,1)`, `_EmissionColor 0`, `_ColorMultiplier 1` — colour comes from the shader/domain, not the material (`Assets/_Graphics/Materials/{Blue,GreenAccent,Screen}VesselMaterial.mat`).

---

## 2. WHAT THE PREFAB REFERENCES INSIDE THE FBX

Nothing stripped, nothing modified — **13 mesh sub-asset ids only**, bound by `MeshFilter.m_Mesh` on prefab-native GameObjects (`TREE`; mapping by collider size/centre, which equal each mesh's FBX AABB ×0.01 to 4 digits):

| prefab node (Unity local scale) | `m_Mesh` fileID | FBX mesh (file +x twin / file −x twin — see §1.4 for which is which) | Unity-authored collider on the node (local, pre-scale) |
|---|---|---|---|
| `Body` (1) | `2959467708922500008` | `Body` / `Sphere.009` | `SphereCollider r 0.2072551 c (0, 0, −0.0008)` |
| `JetTopLeft` / `JetTopRight` (1.75) | `4213887719459583113` / `-4054977461692037399` | `UpperJet` / `UpperJet.003` | `Box (0.0865, 0.0865, 0.1574) c (∓0.1052, 0.1307, 0.0257)` |
| `ShroudTopLeft` / `ShroudTopRight` (1, under the jet) | `8436260602439234206` / `-6511944097885156458` | `UpperJetHold` / `.003` | `Box (0.1082, 0.0957, 0.2558) c (∓0.1215, 0.1536, −0.0142)` |
| `JetBottomLeft` / `JetBottomRight` (1.75) | `3301801484668628847` / `3651206300688657872` | `LowerJet` / `.003` | `Box (0.0602, 0.0602, 0.1095) c (∓0.1273, −0.1297, 0.0124)` |
| `ShroudBottomLeft` / `ShroudBottomRight` (1) | `1173804494966244414` / `-1309588489851300069` | `LowerJetHold` / `.003` | `Box (0.0740, 0.0705, 0.1930) c (∓0.1409, −0.1436, −0.0154)` |
| `LeftGun` / `RightGun` (1.75) | `9134634040662503695` / `8514049938098572669` | `SideShooters` / `.003` | `Box (0.0560, 0.0526, 0.0737) c (∓0.1661, −0.0011, 0.0155)` |
| `ShroudLeft` / `ShroudRight` (1) | `-3318364715813557357` / `-8852780382351287992` | `SideShooterHold` / `.003` | `Box (0.0330, 0.1450, 0.1462) c (∓0.1996, −0.0002, 0)` |

- `ShootPoints` (`Sphere.041`) is referenced by nothing in `P`.
- Each of the 13 `MeshRenderer`s hand-authors `m_Materials` `[BlueBase, GreenAccent(, Screen)]` — the FBX material NAMES are not load-bearing (no `externalObjects`), but the **slot ORDER per mesh is** (slot 1 is what the domain replaces, §5).
- `VesselCustomization._shipGeometries` (`P &7779107667008759293`) lists all 13 GameObjects in the order `Body, JetBottomLeft, ShroudBottomLeft, JetBottomRight, ShroudBottomRight, LeftGun, ShroudLeft, RightGun, ShroudRight, JetTopLeft, ShroudTopLeft, JetTopRight, ShroudTopRight`; `_domainMaterialSlot 1`; `_domainReplacesMaterials [GreenAccentVesselMaterial]`.
- The four `Urchin_*_HullFusionBake.asset` bind `hullMesh → 2959467708922500008` + `hullHash 35758063` + `hullVertexCount 11285` (§1.1).
- **Sub-asset fileIDs are derived from type + name (`M: fileIdsGeneration 2`) and are not derivable offline** (`.claude/skills/asset-surgery/SKILL.md:389-396`; the omni-crystal attempt to borrow an id by renaming failed, 2026-10). Unity names an imported Mesh after the FBX node, so the 13 **node names** (`Body`, `UpperJet`, `UpperJet.003`, `LowerJet`, `LowerJet.003`, `UpperJetHold`, `UpperJetHold.003`, `LowerJetHold`, `LowerJetHold.003`, `SideShooters`, `SideShooters.003`, `SideShooterHold`, `SideShooterHold.003`) must be kept exactly; keeping the geometry names (`Sphere.009/.038/.011/.039/.014/.033/.010/.034/.013/.040/.015/.023/.008`) too costs nothing and removes the only other candidate hash input. One mesh per node, same slot order, same file (same guid) — then all 13 `MeshFilter`s and the four bakes keep binding.
- Things on the vessel that a model swap never touches: everything on the root (24 components), `OrientationHandle`, `Skimmer`, `FarViewCamera`, `VesselTail`, `ShipActions`, `VesselActions`, `ProjectilePool`, `ShipHUDContainer`.

---

## 3. ANIMATION

### 3.1 Driver — code puppetry, `UrchinAnimation : VesselAnimation` (`A`; component `P &4005043449782909879`)
Serialized on the prefab (`TREE`): `SkinnedMeshRenderer {fileID: 0}`, `SaveNewPositions 0`, `brakeThreshold 0.5`, `lerpAmount 2`, `smallLerpAmount 0.7`, **`animationScaler 30`** (C# default 25, `A:23`), and the 13 part fields **authored** to the prefab nodes: `Body, JetBottomLeft, JetBottomRight, ShroudBottomLeft, ShroudBottomRight, ShroudLeft, ShroudRight, LeftGun, RightGun, JetTopLeft, JetTopRight, ShroudTopLeft, ShroudTopRight`. No `animator` field exists on this class.
- **`ResolveParts()` (`A:31-58`)** — an authored reference always wins (`VA:86-88`); when a field is empty the first descendant (case-insensitive, `GetComponentsInChildren(true)`, first occurrence wins) matching these candidates, **in this priority order**:
  | field | candidates (exact strings) |
  |---|---|
  | `Body` | `"fuse"`, `"Body"` |
  | `LeftGun` / `RightGun` | `"gunM.l"`, `"LeftGun"` / `"gunM.r"`, `"RightGun"` |
  | `JetTopLeft` / `JetTopRight` | `"jetT.l"`, `"JetTopLeft"` / `"jetT.r"`, `"JetTopRight"` |
  | `JetBottomLeft` / `JetBottomRight` | `"jetB.l"`, `"JetBottomLeft"` / `"jetB.r"`, `"JetBottomRight"` |
  | `ShroudTopLeft` / `ShroudTopRight` | `"wingconrotT.l"`, `"ShroudTopLeft"` / `"wingconrotT.r"`, `"ShroudTopRight"` |
  | `ShroudBottomLeft` / `ShroudBottomRight` | `"wingconrotB.l"`, `"ShroudBottomLeft"` / `"wingconrotB.r"`, `"ShroudBottomRight"` |
  | `ShroudLeft` / `ShroudRight` | `"sheildconrot.l"`, `"ShroudLeft"` / `"sheildconrot.r"`, `"ShroudRight"` |
  Unresolved parts are reported once by `ReportUnresolvedParts()` (`VA:112-120`) and simply do not move. The shrouds are resolved but **never driven** (`A:41-49`): they move only because they are children of their jet/gun.
- **Rest poses**: `CaptureRestRotations(Body, LeftGun, RightGun, JetTopLeft, JetTopRight, JetBottomLeft, JetBottomRight)` (`A:54-55`, `VA:148-157`) records each part's authored `localRotation` — identity on the shipped art (all nodes rest at identity, `TREE`), so `RotatePartFromRest` (`VA:180-189`: `target = Euler(pitch, yaw, roll) * rest`, `Lerp(local, target, lerpAmount·dt)`) reduces to an absolute Euler drive today. A rigged replacement whose parts rest at non-identity angles is handled by the same capture (the comment at `A:50-53` records why).
- **`PerformShipPuppetry(pitch, yaw, roll, throttle)` (`A:107-154`)**: inputs from `VA:27-35` — two-stick (the Urchin never sets `IsSingleStickControls`; only `SingleStickVesselTransformer`/`ScarabVesselTransformer` do): **pitch = `YSum`, yaw = `XSum`, roll = `YDiff`, throttle = `XDiff` (unused by the puppetry)**. Every driven part gets `Euler(pitch·30, yaw·30, roll·30) · rest` about its PARENT's axes = the vessel's axes (all parts hang under `Body`, which is identity under `OrientationHandle`): the two guns and the four jets at full stick deflect **30° on each axis**; `Body` the same **unless riding**, when `Body` is driven to `Euler(dt·100, 0, 0)` — a ~1.7° nose-down target at 60 fps, i.e. the body holds still while attached. Lerp rate 2/s (63% of the way in 0.5 s).
- **Direction convention**: `VT:921-927` pitches about `transform.right` by `+YSum`; `Euler(90,0,0)·forward = down`, so `YSum > 0` = hull nose DOWN, and the parts get `Euler(+YSum·30, …)` = the **same sense** — pods and body tilt WITH the hull, 30° ahead of it at full stick. Yaw `+XSum` = nose right (`VT:929-935`) → parts yaw right; roll `+YDiff` about `forward` (`VT:937-943`) = bank left → parts roll the same way. Nothing on this hull counter-rotates.
- **Scale puppetry (`A:77-105`)**: `scaledParts` = the four jets; every frame `localScale` lerps (6/s) to `1.75` when not attached and to `1.0` when `VesselStatus.IsAttached` — the pods (jet + child shroud + child plume) **tuck into the ball while riding** and extend in free flight. Guns/side shrouds never scale.
- **Idle / drift**: `VA:32` calls `Idle()` on `InputStatus.Idle`; `Idle()` only resets the `Transforms` list (`VA:322-348`), and `AssignTransforms()` is a no-op (`A:156-161`, all three `Add`s commented out) → **nothing returns to rest on idle**; parts stay wherever the last non-idle frame's lerp left them (the stick has usually decayed to 0 first, so the residual is small but never settles to exactly rest).
- `FlareEngine()/FlareBody()` (`VA:527-549`) write `SkinnedMeshRenderer.materials[3]` / `[0]` — **no-ops on this hull** (`SkinnedMeshRenderer` is null, guarded by `if (SkinnedMeshRenderer)`). Nothing on the Urchin flares.

### 3.2 The Animator that is on the prefab, and what it does (nothing)
`P:307-328`: an `Animator` (`&6795854866240994547`) on the empty GameObject **`Animation`** (`&1189492708410417601`, child of `OrientationHandle`, no children, no renderer) with `m_Controller → C` (`MantaAnimatorController`), `m_Avatar 0`, `m_Enabled 1`, `m_CullingMode 0`. `UrchinAnimation` has no `animator` field and never writes `Pitch/Yaw/Roll/Throttle`; the controller's live state `Blend Tree` (`&-7407348079967575313`, `m_Motion → &-3065510467884305016`, Direct tree over `x`/`y` with children `&1054661598827968095` (Yaw/Pitch, FreeformDirectional2D) and `&-5535465862024685328` (Roll/Throttle)) binds **mantis clips** (guid `7c324ef5…`, `C:342-372, 104-134`) that address bones this GameObject does not have. **Dead weight**: it evaluates a Manta blend tree every frame against nothing. The seven `urchan_*` clip references in `C` (`C:216,225,234,243,408,417,441` → guid `7f9bbc7b…`, fileIDs `-5759537401921551803, -890555747122391063, -8562988994063707841, 5695539142173920075, -8354614058629992318, 3027755657830208708, -3745520805802180186`) live in three ORPHAN trees `urchan fly` (`&-1900624049200794090`), `throtle` (`&1107033417084343733`), `roll` (`&1887696347075263162`) — each referenced exactly once (its own header), unreachable from `Base Layer`. They are why `Docs/VESSEL_CONSTRUCTION.md:1146` counts `R` as "LIVE … nine vessels": deleting `R` empties orphan trees in four controllers, not a state any vessel plays.

### 3.3 Takes on the LIVE model
`F` has **0 takes** and `M.clipAnimations []`. There are no take names to keep for the shipped wiring.

### 3.4 Takes on the unwired rig `R` (for a modeller who chooses the rig path; `PROBE --takes`)
`R.meta` also has `clipAnimations []`, so its clips import under the raw take names. **Eleven takes, ALL MOTIONS** (21 keys over 0.833 s; `urchan_roll` 1.667 s, 18 keys), **each carrying 4 blend-shape curves** (the `CONTRACT.md §7` trap: `VA.LateUpdate` is the single writer of shape weights; animated shape curves fight it). Nodes moved per take: `urchan_ball` 16 (the six `winggrowrot*` bones scale `range 0.75 offset 0.75` — the rig's version of the 1.75→1.0 tuck), `urchan_fly` 16 (identical curves to `ball`), `urchan_pitchdown` 18, `urchan_pitchup` 18, `urchan_roll` 16 (`fuse` rotation range 360 = a full barrel roll), `urchan_rollleft` 16, `urchan_rollright` 16, `urchan_speeddown` 18, `urchan_speedup` 20 (the only take with a translation: `fuse` 0.065), `urchan_yawleft` 16, `urchan_yawright` 16. Deflections (rotation ranges, degrees): pitchdown — `wingconrotB.l/r` 70.5, `jetT.l/r` 41.2, `winggrowrotM.*` 37.5, `root` 13.5; pitchup — `winggrowrotM.*` 50.5, `jetB.*` 42.1, `jetT.*` 36; rollleft/right — `winggrowrotM.*` 83.6–88.4 (the side pods swing), `root` 25.5/30.7; speeddown — `wingconrotT/B.*` 92.2 (the holds fan open as brakes), `jetT/B.*` 47.5; speedup — `wingconrot*` 47.8, `winggrowrot*` 29.6–32.2, `gunM.*` 28.7; yawleft — `wingconrotB.l` 37.4, `wingconrotT.l` 36.3; yawright — `wingconrotB.r` 38.9, `wingconrotT.r` 38. Take names are `Armature|urchan_<name>`. None of these is bound by anything the Urchin plays (§3.2).

### 3.5 Rig bones (`R`, `PROBE`): 38 bones, one skinned mesh `Sphere.042` (6,080 v / 5,506 polys / 12,090 tris, 4 slots {BASE 3,976, accent 861, windsheild 84, N 585}), 38 clusters. Chain per side: `root` → `winggrowrotT.r` (rest S 1.75) → `jetholdT.r` → {`wingrotT.r` → `wingconrotT.r` (skins 316), `jetrotT.r` → `jetT.r` (384)}; `winggrowrotM.r` → `gunholdM.r` → {`gunrotholdM.r` → `gunM.r` (318), `sheildrot.r` → `sheildconrot.r` (426)}; `winggrowrotB.r` → `jetholdb.r` → {`jetrotb.r` → `jetB.r` (384), `wingrotB.r` → `wingconrotB.r` (275)}; `fuse` (1,874) under `root`. Skinned counts equal the live parts' vertex counts exactly (316/384/318/426/275 — `UpperJetHold/UpperJet/SideShooters/SideShooterHold/LowerJetHold`), and the rig's bounds are the live FBX × 2.105 (`PARTMAP`: 0.911/0.873/0.854 vs 0.432/0.415/0.406; `Docs/VESSEL_CONSTRUCTION.md:274-282`). Its four element shapes are **EMPTY** (1 vertex indexed, Δ 0.0000, `MEASURE`). Rest angles the code would have to drive relative to: `gunM.l` (90.14, −0.22, 0.96), `jetT.l` (76.31, 18.41, −2.0), `jetB.l` (74.88, −17.17, 2.72) (`PROBE`).

---

## 4. ELEMENT MORPHS

### 4.1 Current state
**None.** `F` carries no blend shapes (`MEASURE: blend shapes: none`), there is no `SkinnedMeshRenderer`, and `CollectElementShapes` (`VA:499-520`) finds nothing → `InitializeElementMorphs` returns at `VA:433` ("model ships no element shapes"). The rig `R` labels `mass`, `charge`, `space`, `time` and each moves 0 vertices by 0.0000 (`PROBE`, `MEASURE`; `CONTRACT.md:380-385`). The element levels today reach the pilot only through the HUD flowers and the abilities' numbers.
Label rules a replacement must meet (`VesselElementalMorphConfigSO.TryResolveElement`, `Assets/_Scripts/ScriptableObjects/VesselElementalMorphConfigSO.cs:60-98`): the text after the last `.` equals `Charge|Mass|Space|Time` case-insensitively, or exactly one token (split on `_-. `) equals one; shapes on a `SkinnedMeshRenderer`; the last frame's weight is the extreme (`VA:511-512`); `M: blendShapeNormalImportMode 1` (author normals, `asset-surgery SKILL.md §4.8d`); no blend-shape curves in any take.

### 4.2 What each element DOES on this hull (`python3 Tools/Build/element_ability_table.py Urchin`: 4/4 wired, 0 disagreements)
| element | ability | input | scaling L0→L10 | L5 upgrade |
|---|---|---|---|---|
| **Charge** | Chain Spikes (`URCHIN_CHAIN_SPIKES.md`) | RT / R-Shift | `chargeRangeMultiplier` 1 → 2.5 (cascade reach), generations scale with Charge | Overcharge |
| **Mass** | Trail Rider (`URCHIN_TRAIL_RIDER.md`) | passive | `growthAmount` 0.6 → 1.2 (prisms you ride GROW) | Reinforced Wake (ridden prisms come up shielded) |
| **Space** | Track Projector (`URCHIN_TRACK_PROJECTOR.md`) | LT / L-Shift | `ProjectileTime` 4 → 8 and ×1 → ×2 = the projected rail's LENGTH | Long Haul |
| **Time** | Slip (`UrchinSlipActionSO.cs:40`) | B / R | 0.6 → 1.6 absolute (the slip's rate/duration) | Slipstream |

### 4.3 Proposed morphs for the replacement (fleet rule: Space = reach, Time = rate/mobility, Charge = threat, Mass = size). Diagonal reference = the FBX/riding diagonal **72.3 cm** (`PARTMAP` size 0.432/0.415/0.406); target travel 3–15% = **2.2–10.8 cm** of displacement in file units.
| element | reads as | geometric change (file cm; the mesh is authored in the balled pose, so author deltas on the balled hull) | target maxD |
|---|---|---|---|
| **Charge** | the spikes grow — more cascade reach | both gun barrels (`SideShooters`) lengthen +6..8 cm along +Z and sharpen; the 29 ShootPoint sites on the body (`GEOM` list, radius ≈ 20 cm) rise into 2–3 cm cones (urchin spines); the side-ring shrouds' teeth sharpen | **7–8 cm (10–11%)** on the barrels, ≥1,500 verts moving |
| **Mass** | the ball is heavier; what it rides grows | inflate the body sphere r 20.3 → 23 cm along normals (+2.7 cm), thicken the four hold shells +1.5 cm, keep the window discs fixed so the eye looks smaller in a fatter head | **2.7–3.2 cm (4%)** — above the fleet floor (Squirrel Mass 2.46%), on the one part the pilot sees most (the rear of the ball) |
| **Space** | reach — the pods stand further out | translate each of the six pod groups (hold + jet/gun) outward along its own radial +5..6 cm (top pods along (±12, 15, 0) normalised, bottom along (±14, −14, 0), side along ±X); widen the canopy ring +2 cm | **5–6 cm (7–8%)**; riding width 43.2 → ≈ 54 cm |
| **Time** | faster — swept, streamlined | rake the four hold shells aft 25–30° about their inboard edge (tips move −Z 5..7 cm), stretch the jets aft +3 cm, pull the body into a prolate 20.3 × 20.3 × 23 ellipsoid (nose +1.5, tail −1.5 cm) | **5–7 cm (7–10%)** |
Keep every morph OFF the window discs and off the 13 collider-defining AABBs' inboard faces (colliders are not re-fitted by level).

---

## 5. IDENTITY

- **Role** (`Docs/claude/ABOUT_PROJECT.md:18`, vesselType 4 in `P`): "chain-reaction spikes + prismscape rider + a projected rail — and the fleet's only hull that flies BACKWARDS". While riding, the prisms around it DRAPE onto the hull (`PrismCradle`, `PRISM_ANIMATION.md §4.7.2`). Class copy (`Assets/_SO_Assets/Classes/SO_Class_Urchin.asset`): *"Don't mess with me or I will shoot. I will shoot anyway, but don't mess with me."* Modes: `Hijack(46)` (rail heist), `Skein(51)` (cable race) (`GameModes.cs:119,170`). **Doc vs asset**: `ABOUT_PROJECT.md:18` still says "HUD prefab not yet authored"; `P` nests `UrchinHUDVariant.prefab` (`P` PrefabInstance `&7791000000000000010`, shipped 2026-10-06 per `VESSEL_HUD_AND_ELEMENTS.md:196`).
- **Controls / speed class**: two-stick (`GVT`); `DefaultThrottleScaler 65`, `DefaultMinimumSpeed 0`, `MaxBoostMultiplier 5`, `BoostDecayRate 0.1`, `PitchScaler/YawScaler/RollScaler 90` (`TREE` GunVesselTransformer `&975128113266807093`). Throttle is the re-centred signed `XDiff` (`GVT:801-808`): stick centre = **stop**, full forward 65 u/s, full back **65 u/s reverse** (`URCHIN_REVERSE_FLIGHT.md`); on a friendly ribbon it grinds at 300 u/s, hostile 20 (`TrailFollower`/`BlockscapeFollower` on `P`). The hull keeps facing +Z in reverse; only the plumes swing end-for-end (`VesselJet.cs:84-124`, 180° about each jet's local Y) and the wake streams past the nose.
- **What it does to prisms**: lays its own trail (`VesselPrismController BaseScale (10, 2.5, 4)` — a 10-u-wide prism from a 0.4-u ball), steals prisms by spike (`ProjectileStealPrismEffectSO`), grows what it rides, projects a rail of its own domain's mass, shields ridden prisms at Mass 5.
- **Shipped silhouette** (`RENDER shipped_1.75_tail34.png`, `_nose34`, `_side`, `_top`; §1.5): a **near-sphere** (r 20.3 cm) wrapped in flat emissive circuit-board panels (crosses, rings, chevrons, a broken-gear ring on each side), a **three-disc canopy "eye"** filling the nose cap (one large disc, two small), and **six detached pods** floating 1.75× out from the ball: four jet pods (a dome-shaped hold shell with a cross motif cupping a smaller dome "jet" inside) at the top and bottom quadrants, and two side gun pods (a gear-toothed ring shroud holding a short barrel). At rest nothing connects the pods to the ball — they hover (`RENDER` tail34: visible gaps). Riding, the four jet pods sink flush into the sphere and the gun pods stay out (`shipped_1.0_*`: the ball with only the two side gears proud). Card art `Assets/_Graphics/CardImages/Urchin_Square.png` (yellow/cyan, 3/4 nose, pods out) and `Urchin_Square_silhouette.png`; hangar `Assets/_Graphics/Design Assests/Hangar/Urchin.png` (blue ball, black glossy eye, three detached teardrop spines) and `Urchin Trail.png`; icons from `SO_Class_Urchin.asset` (`IconActive d88eff3a…` = `Urchin_Square.png`, `IconInactive 4d7b2e9a…` = `Urchin_Inactive.png`). The motifs to keep: **ball + one big eye + floating pods that extend and retract**.
- **Camera** (`CAM`): `mode 0`, `followOffset (0, 0.83, −6.67)`, `dynamicMinDistance 5`, `dynamicMaxDistance 8.33`, `followSmoothTime 0.2`, `rotationSmoothTime 5`, `nearClipPlane 0.3`, `farClipPlane 12000`; the player camera is **60° vertical FOV** (`Assets/_Prefabs/CORE/CameraManager.prefab:318`). At 6.67 u on a 1080p frame: 935 px per unit of tan → the free-flight hull is **≈106 px wide × 93 px tall**, the ball alone **≈57 px** (`RENDER shipped_1.75_chasecam.png`, rendered at exactly that pose: a ~90 px blob). A feature must be ≥ 2 cm (0.02 u, 3 px) to exist and ≥ 5 cm (7 px) to read; the 2–3 cm accent stripes are 3–4 px, the 8–10 cm crosses/rings 12–14 px. **The pilot sees the REAR**: the two top pods, two bottom pods, two side gears and the ball's aft circuitry; the eye is never seen in play. The tail at z −7 and the 0.334-width ribbons dominate the frame.
- **Domain colour slot**: `VesselCustomization` (`P &7779107667008759293`): `_domainReplacesMaterials [GreenAccentVesselMaterial]` → `HasDomainMaterialIdentities()` true (`VesselCustomization.cs:100-105`) → `ShipHelper.ApplyShipMaterialToSlots` writes the domain material into **every slot that currently holds `GreenAccentVesselMaterial` = slot 1 on all 13 parts** (`VesselHelper.cs:128-153`, via `sharedMaterials`). `_domainMaterialSlot 1` is the fallback for the identity-less path and agrees. Slot 0 stays `BlueBaseVesselMaterial`, slot 2 `ScreenVesselMaterial`. So the team colour is the 25% emissive panel area (29% of the ball's surface) — large and legible at 57 px.

---

## 6. GAMEPLAY MOUNTS (Unity units; local values from `P`/`TREE`; world = local × 1.75 for anything under a jet or gun node)

1. **Jets ×4** — `VesselJet.prefab` (guid `55ceca57…`) instances named `VesselJet (JetTopLeft/JetTopRight/JetBottomLeft/JetBottomRight)` (`P:4426,4520,4614,4708`), parented to the four jet NODES (not bone-mounted; `mountBone` unset). Authored local: top `(∓0.105, 0.131, −0.052)`, bottom `(∓0.127, −0.13, −0.042)`, rotation identity, **`m_LocalScale (0.1145143, 0.1145143, 0.02481143)`** = the fleet reference plume `(0.6, 0.6, 0.13) × 6.67/20` divided back through the node's 1.75 (`VESSEL_TAIL_AND_JETS.md:140-151, 227`), `widthScale 0.334` on all four (`P:4496,4590,4684,4778`). **World mouths in free flight**: top `(∓0.184, +0.229, −0.091)`, bottom `(∓0.222, −0.227, −0.074)`; riding (nodes at 1.0) they collapse to the local values. Plumes point −Z at rest and swing 180° about local Y in reverse (`VesselJet.cs:124`). **The jet meshes have no nozzle** — the plume origin is the jet dome's rear AABB face; a replacement should put a real exhaust mouth at those four points.
2. **Tail ×1** — `VesselTail.prefab` (guid `ef25530d…`) on the ROOT at **`(0, 0, −7.0)`**, identity, `widthScale 0.334` (`P:4330-4402`; `VESSEL_TAIL_AND_JETS.md:227`: camera −6.67, tail z −7.0, width× 0.334).
3. **Skimmer** — `Skimmer.prefab` (guid `6540c785…`) as `Skimmer` under `OrientationHandle` at (0,0,0), prefab-default `m_LocalScale 15` × `SphereCollider r 0.5` trigger = **7.5 u radius** (`Skimmer.prefab:36,65-70`) — 17× the ball; `VesselStatus._nearFieldSkimmer → &874579554510349383`, `_farFieldSkimmer 0`.
4. **Guns / bays** — `Gun` components on `GunContainer` (under `Body`, `firePeriod 0.2`, `sideLength 2`) and on `LeftGun`/`RightGun`. `UrchinSpikeActionExecutor` (`P &7770000000000000007`): `muzzles → MuzzleL, MuzzleR` (bare transforms under the guns at local `(∓3, 0, 4)` → **world `(∓5.25, 0, +7.0)`** — the aimed shotgun/single shots spawn 7 u AHEAD of a 0.4 u hull, nowhere near the barrel geometry at world `(∓0.29, 0, +0.03..+0.09)`); `gun → GunContainer`; `barrageOrigin → OrientationHandle` (the omni barrage fires a sphere from the hull centre). The **30 `Sphere (n)` GameObjects under `GunContainer`** (scale 0.01, no components) are hand-copies of 29 of the FBX `ShootPoints` (rounded to 0.01, one duplicated at `(−0.04, 0.2, 0.02)`, one omitted) and **nothing reads them** (`Gun.cs` never iterates children; `FireSingle` spawns at `containerTransform.position`, `Gun.cs:292`). Legacy of the 2023 shoot-point design; a replacement may drop `ShootPoints`.
5. **Colliders** — all non-trigger, layer 8 `Ships`, each with `ImpactCollider → VesselImpactor &7770000000000000011` (`UrchinImpactorDataContainer.asset`): `Body` `SphereCollider r 0.2073 c (0, 0, −0.0008)`; the 12 appendage `BoxCollider`s of §2 — in each node's own frame the box is the exact AABB of its mesh (sizes match `MEASURE` extents to 4 digits), so **the colliders do bound their parts**, and Unity scales them with the node (×1.75 in free flight, ×1.0 riding). `VesselRigSwapper.cs:199-207` records "~0% overlap … the Urchin's colliders bound nothing" — that control compared against FBX vertex data at node scale 1 against boxes read at prefab scale; the asset shows a per-node exact fit. Root `Rigidbody` + `ClientNetworkTransform` untouched by a swap.
6. **Rear camera** — `PipCamera.prefab` as `FarViewCamera` under `OrientationHandle` at `(0, 0, −70)` (`TREE`), active.
7. **Crystal-hull fusion** — the four bakes bind the `Body` mesh id + `hullHash 35758063` + `hullVertexCount 11285` over 13 parts; any re-export makes them **STALE** (fallback solves on a worker thread with a `WarnOnce`) → re-run **FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions** after the swap.

---

## 7. IN-FLIGHT WORK (`git fetch origin`; `python3 Tools/Build/who_works_where.py query …`, 2026-10-17)

- `query Assets/_Prefabs/Spacevessels/Urchin.prefab "Assets/_Models/Vessel Models/Urchan_Test.fbx" Assets/_Scripts/Controller/Animation/UrchinAnimation.cs` → **IN FLIGHT: none. LANDED on bleeding-edge (30 days): none.**
- `query Assets/_SO_Assets/Camera/UrchinCameraSettingsSO.asset "…/urchan_shapekey_with_animations.fbx" Assets/_Models/Animations/MantaAnimatorController.controller Assets/_Scripts/Controller/Vessel/GunVesselTransformer.cs` → **none / none.**
- No branch to diff. Last touches (`git log`): `P` via merge #999 `cece/fervent-gates-ychb6q` (Garrett Milliron, 2026-10-07); `F`/`M` 376b525b1 "feat(crystal): bake crystal hull fusions" (Garrett, 2026-10-08 — the `isReadable 1` flag); `A` f7ce592bb 2026-10-04. A model swap collides with nothing in flight; the generators that READ `P` and must stay green afterwards: `Tools/Build/author_urchin_hud.py --check` (asserts HUD fileIDs and the executor ids, `:664-672`), `Tools/Build/skein_budget.py` (reads vessel facts from `P`, `:223,1564`), `Tools/Build/regatta_balance.py:139`. None reads mesh ids or transforms of the 13 part nodes.

---

## 8. CONSTRAINTS FOR A REPLACEMENT (numbered, exact)

1. **File**: write **in place** over `Assets/_Models/Vessel Models/Urchan_Test.fbx`, same guid `fee259a2cc152ca409c9c85a43ecc91e`; leave `M` untouched (`isReadable 1`, `importBlendShapes 1`, `fileIdsGeneration 2` are all needed). FBX 7400 binary, **`UnitScaleFactor 1.0` (centimetres), `UpAxis +Y, FrontAxis +Z, CoordAxis +X`** exactly as shipped; every node at identity TRS with all geometry in the one model frame (the prefab places and scales the nodes).
2. **Frame/size**: nose = file **+Z**, up = **+Y**, file **+X = pilot's LEFT after Unity's x-mirror** (§1.4; confirm the L/R pairing once in the editor). Match the **balled** hull: body sphere r **20.3 cm** centred at the origin (collider r 0.2073 u), window cap at z +20.65; appendages within their AABBs of §1.5 ±10% so the 12 box colliders (pre-scale local frames) still enclose them: top pods x 6.7..17.6, y 10.6..20.2, z −14.2..11.4; bottom pods x 10.4..17.8, y −17.9..−10.8, z −11.2..8.1; side gear x 18.3..21.6, |y|,|z| ≤ 7.3; barrels x 13.8..19.4, z −2.1..5.2. Whole-model riding bounds 43.2 × 40.6 × 41.5 cm (diag 72.3); the prefab's 1.75 makes free flight 0.756 × 0.666 × 0.455 u — author the pods so they still look attached-by-design when pushed out 1.75× (a stalk/socket that reads at 1.0 AND 1.75, or geometry that overlaps the ball at 1.0 and clears it at 1.75).
3. **Node names to keep (exact, 13 meshes, one mesh each, same slot order)**: `Body`, `UpperJet`, `UpperJet.003`, `LowerJet`, `LowerJet.003`, `UpperJetHold`, `UpperJetHold.003`, `LowerJetHold`, `LowerJetHold.003`, `SideShooters`, `SideShooters.003`, `SideShooterHold`, `SideShooterHold.003`; keep the geometry names `Sphere.009/.038/.011/.039/.014/.033/.010/.034/.013/.040/.015/.023/.008` as well (cheap insurance on the id hash). `ShootPoints` may go. Do NOT add an armature or rename nodes on the in-place path — the prefab is part-per-mesh and `UrchinAnimation` resolves the prefab GameObject names, not FBX names.
4. **Takes**: none to keep (0 on `F`; the on-prefab Animator binds mantis clips on an empty GameObject). Do not add takes with blend-shape curves.
5. **Bones the code resolves**: none on the live path (all 13 fields are authored to prefab nodes). If a future rig path is taken, `A:33-49` resolves `fuse`, `gunM.l/.r`, `jetT.l/.r`, `jetB.l/.r`, `wingconrotT.l/.r`, `wingconrotB.l/.r`, `sheildconrot.l/.r` (case-insensitive) and captures rest rotations for the seven driven ones.
6. **Slot order and meaning on every mesh**: **0 = base hull** (`BlueBaseVesselMaterial`), **1 = DOMAIN accent** (`GreenAccentVesselMaterial`, replaced at spawn by the team material, emissive panels — keep it ≥ 20% of polys and on the REAR-facing surfaces), **2 = window** (`Body` only, `ScreenVesselMaterial`). Material names are free (no `.meta` remap), order is not. No slot 3; `FlareEngine/FlareBody` are inert here.
7. **Morphs**: not consumable on this prefab (no `SkinnedMeshRenderer`, `CollectElementShapes` reads only skinned meshes, `VA:501`). Shipping shapes requires the skinned path (one skinned `Body` with bones for the pods, `_shipGeometries` and `_domainReplacesMaterials` re-pointed, the 12 colliders re-homed) = the editor `VesselRigSwapper` pass, a separate change. On the in-place path author the four shapes anyway (names `Charge/Mass/Space/Time`, §4.3 magnitudes, normals) so the file is ready; they are harmless on `MeshFilter`s.
8. **Topology/UV**: close the Body (today 102 boundary / 125 non-manifold edges), ≤ 5% n-gons, one UV layer in [0,1] per mesh (repack `SideShooterHold`'s 1.8%×10.7% island), `weldVertices 1` tolerant.
9. **Verify**: `assimp info` before/after (13 meshes, 3 materials, 0 animations, bounds ±21.6/±20.3/±20.8); `python3 Tools/Build/vessel_model/fbx_probe.py "<F>"`; `python3 Tools/Build/measure_vessel_models.py --only Urch`; `python3 Tools/Build/author_urchin_hud.py --check`; in the editor: reimport, read `JetTopLeft`'s mesh name, `FrogletTools > Vessels > Audits > Audit Vessel Tails and Jets` (four jets resolve, plume scale on target), `Audit Vessel Construction`, then **`FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions`**; fly: pods extend on launch and tuck on a ribbon, plumes sit in the four mouths, reverse swings them.
10. **Wiring path: in-place overwrite** (same guid, same 13 node names) — zero prefab edits. **Risk**: a renamed or re-typed node silently empties that `MeshFilter` (an invisible part, no error), and the four hull-fusion bakes go stale until re-baked. The alternative (new file + editor re-point of 13 `MeshFilter`s + 4 bake refs + 12 collider re-fits) is strictly more editor work for no gain; the rig path (`VesselRigSwapper`, `InstanceScale 0.474905`) additionally changes the family (skinned, bones) and — because the swapper fitted the rig to the FBX at node scale 1 — lands the pods at the BALLED radius while the shipped rest shows them at 1.75×; whether the rig's `winggrowrot*` rest scale (1.75) reproduces the push-out is not provable offline.

---

## 9. QUALITY BAR

**Keep (measured strengths):** a unique, instantly readable silhouette (ball + eye + six floating pods) that matches the card/hangar art; a real gameplay gesture already in the art and code (pods out in flight, flush when riding — `A:77-105`, the rig's `urchan_ball` take says the same); the domain slot is **25% of polys / 29% of the ball's area** in big flat emissive panels, legible at 57 px; consistent 2-slot order on every part and 3 on the body; all 12 appendages are closed manifolds with exact-AABB colliders; the 13 mesh/13 collider/4 jet bindings are name-stable on an in-place write; polycount is honest for a close-camera hull.

**Weak (measured):**
- The pods **float**: nothing joins them to the ball at 1.75 (`RENDER tail34`); the "jet" is a closed dome with no exhaust mouth, the plume sits on an AABB face (§6.1); the spike muzzles are 7 u ahead of barrels that are 7 cm long (§6.4).
- The eye — the model's one face — is on the nose the pilot never sees; the rear (what the pilot sees for the whole match) is undifferentiated circuitry on a sphere.
- `Body` is not watertight (102 boundary edges, 125 non-manifold, 226 n-gons of 1,350); 352 n-gons overall; `SideShooterHold` UVs in a 2%×11% corner.
- No armature, no shapes, no takes; `UrchinAnimation` can only tilt whole parts ±30° and scale the jets; shrouds are passengers; idle never returns to rest (§3.1).
- Prefab baggage a swap will inherit: an orphan `Animator` running `MantaAnimatorController` on an empty GameObject (§3.2), 30 dead `Sphere (n)` transforms (§6.4), part colliders authored in pre-scale frames.
- Docs disagree with the asset in two places (§1.1 "rig swapped", §5 "HUD not authored").
- **Polycount**: 11,824 tris / 5,968 verts (11,285 in Unity) for a hull that is ~100 px on screen at its own chase camera, but also the hangar/card close-up. Target for the replacement: **5,000–9,000 tris total, ≤ 11.8k**, spent on a watertight ball with a modelled rear face (a rear "eye"/vent ring the pilot can read at 57 px), four real exhaust mouths at the §6.1 points, pods with a stalk/socket so 1.0↔1.75 reads as extend/retract rather than teleport, and barrels with a muzzle; keep every accent panel ≥ 5 cm wide (7 px) and the domain share ≥ 20%.
