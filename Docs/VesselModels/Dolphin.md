# Dolphin — MODEL DOSSIER (measured 2026-10-17, branch ccr-d93a6dfb-ugbxng = origin/bleeding-edge on every file named here)

> **Record of the ARTIST file as shipped before 2026-10-17.** On 2026-10-17 the file this dossier measures was
> rebuilt in place by `python3 Tools/Build/vessel_model/build.py dolphin` (spec `Tools/Build/vessel_model/specs/dolphin.py`,
> skill `/vessel-model`): same path and guid, same node, geometry, bone and material names, every bone's rest pose
> reproduced to 0.000 (`rest_compare.py`). Sections 1-7 describe the artist file the spec was written against and
> stay the explanation of WHY the spec keeps each name and number; section 8 is the contract the rebuilt file meets.
> Paths under `<scratchpad>/` were the measuring session's temporary files: the commands beside them regenerate
> every number from the repo.


Written for the modeller who will REPLACE the Dolphin's model with a better one on the same prefab, same gameplay, same code.
Every number below was measured from the asset or the code; where a doc disagreed with an asset, the asset is reported and the doc is named.
Scratch evidence: `<scratchpad>/` — `probe/dolphin_shapekey_with_animations.txt` (fbx_probe --takes), `probe/dolphin_live.json`, `dolphin_prefab_tree.txt` (annotated prefab hierarchy), `dolphin_bpy_measure.txt` (Blender import in Unity vessel space), `renders/dolphin_live.png` (Cycles), `tools/prefab_tree.py`, `tools/bpy_measure_dolphin.py`.

Coordinate convention used throughout: **vessel space** = Unity units in the Dolphin root's frame, +Z nose, +Y up, +X starboard. FBX → vessel space is `(−x, y, z) / 100` (`Assets/_Scripts/Editor/VesselRigSwapper.cs:100`; `Tools/Build/verify_vessel_rig_puppetry_frames.py:550` "x mirrored into Unity's frame"), re-proved here: the verifier's constants `wing.l (−0.96897, 0, −0.11397)`, `jetT.l (−0.26948, 0.39609, −1.90064)` reproduce from a Blender import of the FBX to 4 decimals (`dolphin_bpy_measure.txt`).

---

## 1. LIVE MODEL

| fact | value | measured at |
|---|---|---|
| prefab | `Assets/_Prefabs/Spacevessels/Dolphin.prefab` (3,652 lines) | `wc -l` |
| live FBX | `Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx`, 2,586,291 bytes, FBX 7400 | `ls -la`, probe line 1 |
| FBX guid | `3f1757f09e5b9be478831ec707bdfc51` | `.meta` line 2 |
| how the prefab draws it | ONE nested `PrefabInstance &1477261064774053533` (prefab line 2135), `m_SourcePrefab {fileID: 100100000, guid: 3f17…}` (line 2271). No direct mesh/material refs anywhere else in the prefab: the FBX guid occurs 48 times, all inside that block and its 19 `stripped` stubs (lines 2143–2382) | `grep -n 3f1757…` |
| host GameObject | `m_TransformParent {fileID: 2273877253388787683}` = Transform of **`OrientationHandle`** (GO `6807910618444506966`, identity TRS, child of the root `Dolphin` GO `7014513329719026904`, identity TRS, scale 1) | prefab line 2141; tree lines 1–2, 369–370 |
| instance transform overrides | root Transform (cso `−8679921383154817045`): `m_LocalPosition (0,0,0)`, `m_LocalRotation (0,0,0,1)`, euler hint 0. **No scale override** (the import's scale stands). Root GO (cso `919132149155446097`) `m_Name = dolphin_shapekey_with_animations`. SkinnedMeshRenderer (cso `2712662977449985951`) `m_UpdateWhenOffscreen = 1`. `m_RemovedComponents []`, `m_RemovedGameObjects []`, `m_AddedComponents []`, **16 `m_AddedGameObjects`** (§2) | prefab lines 2142–2270 |
| other legacy art | `Dolphin_Test.fbx` (783,356 B, 17 part meshes, no bones/shapes) is NOT drawn: the only `Dolphin_Test` left in the prefab is a native GameObject (line 1964) that hosts the hull BoxCollider (§6); it has no renderer | tree line 428–436 |

### 1.1 Units, scale, axes (measured, not assumed)

- FBX `GlobalSettings`: **`UnitScaleFactor = 1.0`** (file units are centimetres), `up = +Y`, `front = +Z` (probe line 3: `up=+1 front=+2 coord=+0`). NOT 100.
- The exporter (Blender) baked the Z-up→Y-up conversion into the two root nodes: `dolphin [Null] R=(−90,0,0) S=(100,100,100)` and `dolohin [Mesh] R=(−90,0,0) S=(100,100,100)`; vertex data is stored at 1/100 (metres): mesh-local bounds `(−2.644, −2.827, −0.5675)..(2.644, 2.471, 0.6417)`, diag 7.582 (probe lines 5, 34–36).
- `.meta`: `globalScale 1`, `useFileScale 1`, `useFileUnits 1`, `bakeAxisConversion 0`, `optimizeGameObjects 0`, `isReadable 1`, `importBlendShapes 1`, `animationType 2` (Generic), `clipAnimations []`, `sortHierarchyByName 1`, `optimizeBones 1`, `fileIdsGeneration 2` (lines 38–77).
- Net Unity size = metres × 100 (node) × 0.01 (file scale) = **1 : 1**. The armature root and the mesh node therefore carry `localScale 100` in Unity and every bone has lossyScale ≈ 100 (RiptideAnimation.cs:72–75); every GameObject parented under a bone compensates with `localScale 0.01` (collider hosts) or `0.006/0.0013` (jets) — see §6.
- **Hull bounds in vessel space (Unity units)**: x `[−2.6436, +2.6436]`, y `[−0.5675, +0.6417]`, z `[−2.4707, +2.8270]` → **5.287 wide × 1.209 tall × 5.298 long**, diagonal 7.582 (`dolphin_bpy_measure.txt` line 5, calibration 0.99999 against the probe). `measure_vessel_models.py --only dolphin`: `extent (5.287, 5.298, 1.209)`. Docs agree: `VESSEL_CONSTRUCTION.md` §4.3 bbox `(−264.363, −56.747, −247.074)..(264.363, 64.173, 282.702)` in FBX cm.
- **Nose axis = FBX world +Z = Unity +Z.** Evidence: jaw clusters `jaw.u`/`jaw.b` occupy z `[0.832, 2.827]` (the z maximum), the six nozzle clusters z `[−2.29, −1.89]` (the z minimum), wings z `[−0.617, 0.153]`; the flight model flies `transform.forward` (+Z); the camera sits at `(0, 0, −20)` and the tail at `z −21` behind. In the mesh's OWN (Blender, Z-up) coordinates the nose is **−Y** (mesh y = −2.827); the node's −90° X rotation turns that into FBX +Z, and Unity keeps Z and mirrors X. RiptideAnimation.cs:160–161 names the same ends: "the hull's −2.471 tail and +2.827 nose".

### 1.2 Node hierarchy (FBX, exact names; `[skins N]` = vertices with weight > 0.5 on that bone; head = bone head in vessel space)

```
dolphin [Null]  R(−90,0,0) S(100)            ← armature root (Unity: GameObject "dolphin", localScale 100)
  root        head (0, 0, −0.942)            skins 0
    fuse      head (0, 0, 0)                 skins 2763   fuselage; cluster x±0.326 y[−0.567,0.642] z[−2.471,0.977]
      jaw.b   head (0, −0.353, 0.932)        skins 872    lower jaw; cluster x±0.110 y[−0.421,−0.050] z[0.832,2.827]
      jaw.u   head (0, +0.359, 0.932)        skins 872    upper jaw; mirror of jaw.b
    root.001  head (0, 0, 0.421)             skins 0
      winghold.r  head (0,0,−0.336) rest R(−102.92, 0, 0)        skins 0
        wing.r    head (0.969, 0, −0.114)    skins 294    cluster x[0.716,2.644] y±0.075 z[−0.617,0.153], centroid (0.994, 0, −0.304)
      root.002    head (0,0,−0.336)          skins 0
        jetholdm.r / jetholdT.r / jetholdB.r   head (0, 0.183, −1.899) — ONE shared origin for all six holds, skins 0
          jetm.r  head (0.373, 0.201, −1.896) skins 538   engine CASE middle-right; cluster z[−2.241,−1.892]
            jetinm.r head (0.497,0.225,−2.122) skins 712  exhaust BELL middle-right; cluster z[−2.285,−2.109]
          jetT.r  head (0.270, 0.396, −1.901) skins 538   case top-right
            jetint.r head (0.359,0.485,−2.126) skins 712  bell top-right
          jetB.r  head (0.350, −0.019, −1.892) skins 538  case bottom-right
            jetinb.r head (0.467,−0.067,−2.117) skins 712 bell bottom-right
        jetholdm.l / jetholdT.l / jetholdB.l → jetm.l/jetT.l/jetB.l → jetinm.l/jetint.l/jetinb.l   (x-mirrored, same counts)
      winghold.l  head (0,0,−0.336) rest R(77.08, 0, −180)       skins 0
        wing.l    head (−0.969, 0, −0.114)   skins 294
dolohin [Mesh]  R(−90,0,0) S(100)  geometry "Chassis"  SkinnedMeshRenderer, 28 clusters   ← NOTE the typo: the GameObject is "dolohin", the Mesh asset is "Chassis"
```
(probe lines 5–33; bone heads/clusters `dolphin_bpy_measure.txt` lines 8–36.) Bone count 28; 30 model nodes.

### 1.3 Polycount, materials, UVs

- **11,113 polygons** = 10,454 quads + 216 triangles + 443 n-gons (5- to **72**-gons: 104×5, 84×6, 38×8, 36×16, 12×22, 6×30, 6×32, 4×72 …) = **25,118 triangles** after fan triangulation; **12,595 source vertices**; Unity's imported vertex count is **32,267** (split by normals/UVs — `Assets/_SO_Assets/CrystalHullFusion/Dolphin_Mass_HullFusionBake.asset` `hullVertexCount: 32267`). (`dolphin_bpy_measure.txt` POLYGONS line; probe line 34.)
- One mesh, but **17 loose parts** (connected components: the 17 legacy part meshes joined, not welded), **36 boundary edges** (open holes), 36 non-manifold edges. Twelve 13-gons carried zero-length edges that Unity DISCARDED; repaired in place (`Docs/VESSEL_CONSTRUCTION.md` §4.7; gate `Tools/Build/repair_fbx_selfintersecting_polygons.py --check`). Twelve zero-area triangles remain (kept by Unity, draw nothing).
- **UVs**: one layer `UVMap`, range u `[−0.104, 1.000]`, v `[−0.489, 1.000]` — not packed into 0..1, i.e. untextured flat-colour art. No vertex colours.
- **Material slots** (FBX order = Unity submesh order; polygon counts ByPolygon; areas in vessel u²):

| slot | FBX material | FBX authoring | Unity material (`.meta externalObjects`) | polys | area | role as consumed |
|---|---|---|---|---|---|---|
| 0 | `accent.001` | diffuse black, **emissive (1, 0.395, 0)** | `Assets/_Graphics/Materials/GreenAccentVesselMaterial.mat` (guid 539a8c65…) | 3,078 | 3.293 | **DOMAIN** — `VesselCustomization._domainReplacesMaterials = [GreenAccentVesselMaterial]` resolves the slot by identity → runtime repaints slot 0 with the domain ship material. Also `FlareBody()` = `materials[0]` |
| 1 | `BASE.001` | black | `BlueBaseVesselMaterial.mat` (54e78ce1…) | 7,492 | 12.809 | **BODY**, never repainted |
| 2 | `windsheild.001` | diffuse (0.056, 0.058, 0.8) | `ScreenVesselMaterial.mat` (923d3665…) | **10** | 0.068 | **WINDOW** (practically no cockpit read: 10 polys) |
| 3 | `N.001` | black, emissive (0, 0.211, 1) | **NOT remapped** — importer-embedded material from the FBX description | 533 | 0.177 | **ENGINE** glow on the bells; `FlareEngine()` = `materials[3].SetFloat("_ColorMultiplier", 5)` |

All three remapped materials use `VesselGraph.shadergraph` (guid 0236447d…) with `_ColorMultiplier 1`. Slot 3's embedded material is not a VesselGraph material, so `_ColorMultiplier` has no property to land on and the boost engine flare (`VesselTransformer.cs:1477`) is, as far as the assets show, a silent no-op on this hull — unverified in-editor, listed as a risk in §8. `_domainMaterialSlot = 1` on the prefab is IGNORED because the identity list is set (VesselCustomization.cs tooltip; `VESSEL_CONSTRUCTION.md` §4.6: slot 1 here is the BODY, so the index path would paint the whole hull).

---

## 2. WHAT THE PREFAB REFERENCES INSIDE THE FBX

Every reference is by the importer's hashed sub-asset fileID (`fileIdsGeneration: 2`, `internalIDToNameTable: []`), so the ids are not derivable offline (asset-surgery SKILL.md §4.8c); the bone each id stands for is inferred from (a) `VesselRigSwapper.cs:116–137`, which re-homed each named legacy object onto the named bone and each jet onto the named nozzle bone, and (b) each VesselJet's `mountBone` override (prefab lines 2466–2937) matching its `m_AddedGameObjects` host. **A replacement must keep every name in the right-hand column so the same id is regenerated.**

| cso fileID | FBX object (type) | what hangs on it in the prefab |
|---|---|---|
| `919132149155446097` | root GameObject `dolphin_shapekey_with_animations` (asset root) | `m_Name` override |
| `−8679921383154817045` | root Transform | position/rotation overrides (identity) |
| `6162015126245842793` | GameObject **`dolohin`** (the skinned mesh GO) | `VesselCustomization._shipGeometries[0]` (prefab line 1704) |
| `2712662977449985951` | SkinnedMeshRenderer on `dolohin` | `RiptideAnimation.SkinnedMeshRenderer` (line 1596); `m_UpdateWhenOffscreen` override |
| `−8422683393738640149` | Mesh **`Chassis`** | `hullMesh` of all four `Assets/_SO_Assets/CrystalHullFusion/Dolphin_{Charge,Mass,Space,Time}_HullFusionBake.asset` (`hullHash 1963952803`, `hullVertexCount 32267`) |
| `−5003871971731744746` | Transform `jaw.u` | native GO `TopNose` (BoxCollider + ImpactCollider) |
| `−4221225292483353412` | Transform `jaw.b` | native GO `bottomNose` (BoxCollider only) |
| `−4313940321992702145` | Transform `wing.l` | native GO `LeftWing` |
| `53829803120445000` | Transform `wing.r` | native GO `RightWing.001` |
| `1678193489172429003` | Transform `jetT.l` | native GO `Engine case Left.1` |
| `7548178165438796544` | Transform `jetT.r` | native GO `Engine case Right.1` |
| `−8919738492257622398` | Transform `jetm.l` | native GO `Engine case Left.2` |
| `2095503617024300501` | Transform `jetm.r` | native GO `Engine case Right.2` |
| `−3433074543137826216` | Transform `jetB.l` | native GO `Engine case Left.3` |
| `7574257847217725806` | Transform `jetB.r` | native GO `Engine case Right.3` |
| `−555540679654791657` | Transform `jetint.l` | nested `VesselJet (Engine case Left.1)` (`mountBone jetint.l`) |
| `−726818501163874150` | Transform `jetinm.r` | nested `VesselJet (Engine case Right.2)` (`mountBone jetinm.r`) |
| `151156721315266023` | Transform `jetinm.l` | nested `VesselJet (Engine case Left.2)` |
| `1777507748522082473` | Transform `jetint.r` | nested `VesselJet (Engine case Right.1)` |
| `−1923869225315705251` | Transform `jetinb.l` | nested `VesselJet (Engine case Left.3)` |
| `6948007125661694782` | Transform `jetinb.r` | nested `VesselJet (Engine case Right.3)` |

- No `m_Materials.Array.data[N]` overrides exist in the block → the material array is the import's (§1.3).
- No stripped component is a MonoBehaviour from the FBX (the FBX contributes only GameObjects, Transforms, the SkinnedMeshRenderer and the Mesh).
- **Nothing references the animation clips**: the `.meta` has `clipAnimations: []`, the prefab has **no Animator** (`grep -c '!u!95' = 0`), no `.controller` mentions the Dolphin.
- `RiptideAnimation`'s 13 part fields are all `{fileID: 0}` (prefab lines 206–218 of the tree) — resolution is BY NAME at runtime (§3).

**Exact node names a replacement must keep** (same file, same name, same type ⇒ same fileID): `dolphin_shapekey_with_animations` (asset root, from the filename), `dolphin` (armature), `dolohin` (skinned-mesh GameObject — yes, with the typo), `Chassis` (Mesh), `root`, `fuse`, `jaw.u`, `jaw.b`, `root.001`, `root.002`, `winghold.l`, `winghold.r`, `wing.l`, `wing.r`, `jetholdT.l`, `jetholdm.l`, `jetholdB.l`, `jetholdT.r`, `jetholdm.r`, `jetholdB.r`, `jetT.l`, `jetm.l`, `jetB.l`, `jetT.r`, `jetm.r`, `jetB.r`, `jetint.l`, `jetinm.l`, `jetinb.l`, `jetint.r`, `jetinm.r`, `jetinb.r`. (`root`, `root.001`, `root.002`, `*hold*` are not referenced by the prefab or code, but `ResolvePart`'s "first occurrence wins" dictionary and `optimizeBones` make renaming them pointless risk; keep them.)

---

## 3. ANIMATION — code puppetry (`RiptideAnimation : VesselAnimation`), no Animator

**Driver**: `Assets/_Scripts/Controller/Animation/RiptideAnimation.cs` (736 lines) on the `Dolphin` root GO (prefab line 1589). Serialized on the prefab (tree lines 200–232): `SaveNewPositions 1`, `brakeThreshold 0.65`, `lerpAmount 2`, `smallLerpAmount 0.7`, `driftWingForward 3.5`, `driftJetBackwardTotal 1.25`, `wingAuthoredPivot (0,0,0.1)`, `wingFlightPivot (0,0,0)`, `wingStationForward 0.5811`, `wingThrottleNeutral 0.5`, `wingThrottleSweep 66`, `thrusterAuthoredPivot (0, 0.14725685, −2.0470345)`, `thrusterFlightPivot (0, 0.15, −1.7)`, `thrusterAnimationScaler 75`, `mirrorAppendageRoll 0`, `JawResourceIndex 0`, `MinJawAngle 4.7636`, `MaxJawAngle 23.4287`.

### 3.1 ResolvePart candidates (exact strings, priority order; case-insensitive, first matching descendant of the Dolphin root wins — `VesselAnimation.cs:86–105`) — `RiptideAnimation.cs:305–322`

| field | candidates |
|---|---|
| Chassis | `fuse`, `Chassis`, `Dolphin_Test` |
| LeftWing | `wing.l`, `LeftWing` |
| RightWing | `wing.r`, `RightWing.001`, `RightWing` |
| ThrusterTopLeft / TopRight | `jetT.l` / `jetT.r`, then `Engine case Left.1` / `Right.1` |
| ThrusterLeft / Right | `jetm.l` / `jetm.r`, then `Engine case Left.2` / `Right.2` |
| ThrusterBottomLeft / BottomRight | `jetB.l` / `jetB.r`, then `Engine case Left.3` / `Right.3` |
| topJaw AND NoseTop | `jaw.u`, `TopNose` |
| bottomJaw AND NoseBottom | `jaw.b`, `bottomNose` |

Trap: the legacy fallback names (`TopNose`, `bottomNose`, `LeftWing`, `RightWing.001`, `Engine case *`, `Dolphin_Test`) all still EXIST in the prefab as collider-host GameObjects. If a replacement drops a bone name, resolution silently lands on the collider host under the bone (or on `Dolphin_Test`, the hull collider) and the SKIN stops moving while no warning fires. Keep the bone names.

### 3.2 Rest poses and frames
- `CaptureRestRotations` on all 13 parts and `CaptureRestPositions` on chassis + wings + six cases, at `Initialize` (lines 328–336). Every turn is `rest ∘ turn`, conjugated into the **vessel's** frame (`RotatePartFromRestInFrame`), and positions are exact world-space round trips through any bone-chain scale (`MovePartInFrame`, proven by `verify_vessel_rig_puppetry_frames.py` check 8). Therefore a replacement's bone REST ROTATIONS are free (the shipped fan-outs — `winghold.l (77.08, 0, −180)`, `jetT.l (−2.018, 172.348, 57.201)`, `jetB.r (3.568, 5.525, −58.448)` … — do not need to be reproduced), but bone HEAD POSITIONS are load-bearing (§6: jets and collider hosts are children at fixed local offsets, and `wingAuthoredPivot`/`thrusterAuthoredPivot` were measured against these heads).
- Chassis pivot = `fuse` rest position, read at runtime (= origin on this rig).

### 3.3 What moves, about which axes, by how much (all Euler in the VESSEL frame; inputs are InputStatus axes in −1..1, throttle = XDiff in 0..1 with 0.5 cruise)
- `animationScaler = 25`, `exaggeratedAnimationScaler = 75` (lines 60–61).
- **Chassis (`fuse`)**: `Euler(pitch·25, yaw·25, roll·25)` about its own origin; whole hull (jaws ride with it) (line 459–463).
- **Wings** (`wing.l/r`): own turn `Euler(Brake(th)·25, yaw·75 ± sweep, (roll ± pitch)·25)` where `Brake(th) = th − 0.65` if `th < 0.65` else 0 (pitch nose-down on brake), `sweep = max(0, th − 0.5)·66` degrees (right +, left −: back-sweep at throttle), roll term + for right/− for left. Orbit: chassis turn about the fuse, own turn about the flight pivot `(0,0,0)` with station `+0.5811 z`, geometry carried from the authored pivot `(0,0,0.1)` → at zero input the wings draw 0.1 aft of rest + 0.5811 forward (`ChainPosition`, line 640–642). Measured limits on the shipped art: wing roots enter the fuselage at **33.93°** of wing yaw; 66°/unit keeps every wing vertex clear across the throttle range (tooltips lines 158–185; verifier lines 737–760).
- **Six engine cases** (`jetT/jetm/jetB × .l/.r`): one shared own turn `Euler(pitch·75, yaw·75, roll·75)` about the shared flight pivot `(0, 0.15, −1.7)`, geometry from the authored pivot `(0, 0.14726, −2.04703)` → at zero input the cases sit `+0.347 z` forward of their rig rest (bleeding-edge's drawn station z `−1.898..−1.540` for case bodies whose rig clusters are z `−2.290..−1.887`). The bells (`jetin*`) are children and ride along.
- **Jaws** (`jaw.u`/`jaw.b`): NOT stick-driven. `localRotation = Euler(∓angle, 0, 0) · rest`, `angle = atan(lerp(tan 4.7636°, tan 23.4287°, energy))` (lines 290–296, 709–713); upper jaw negative X (opens up), lower positive (opens down). Driven by `ResourceSystem.Resources[0]` (Energy) through `OnResourceChange`. Hinge = bone head `(0, ±0.355, 0.932)`; jaw tip at z 2.827 ⇒ 1.9 u lever ⇒ ±0.75 u tip travel at full gape. These two numbers MUST equal the crystal blast's gape half-angle: `atan((400/2)/2400)` and `atan((2080/2)/2400)` from `DolphinVesselExplosionByCrystalEffect` + `AOEConicExplosion.prefab` (tooltips lines 253–265) — the hull IS the blast gauge.
- **Drift** (`VesselStatus.IsDrifting`): the fuselage+jaws keep aiming; wings and six cases are written EXACTLY in a Course-aligned cage `FromToRotation(forward, Course) · rotation` (antipode hold, 360°/s slew limit), blending from the adopted entry pose to rest orientation + offset: wings `+3.5 z` (the measured true-clearance lunge: the jaw tip sweeps a **2.835 u** sphere about the origin), cases `−1.25 z`; exponential blend at `lerpAmount 2` (lines 488–598). `DriftHandle` GO survives as an inert empty.
- **Idle**: rotations lerp to rest at 0.7/s; positions settle to the resting layout; a stick-idle drift stays in the cage (lines 411–437).
- **Sign convention**: the puppetry applies **no sign flips** (lines 440–457): pitch/yaw/roll are passed through as the flight model emits them; `mirrorAppendageRoll` ships OFF. So the only orientation requirement on a replacement is that its REST pose is the ship's own (nose +Z, up +Y, starboard +X) — the code's conjugation makes bone-local axes irrelevant.

### 3.4 The FBX's ten takes (all MOTION, none consumed)
`fbx_probe --takes` (probe lines 40–245): every take is 0.833 s, 21 keys (20 frames @ 24 fps), curves on 29 nodes:

| take | nodes that move | largest deflections |
|---|---|---|
| `dolphin\|jawcharge_dolphin` | 2 | `jaw.u` 46.7°, `jaw.b` 46.2° (a gape; code uses 4.76°–23.43° instead) |
| `dolphin\|pitchup_dolphin` / `pitchdown_dolphin` | 24 | wings 55–56°, cases 23–58°, `fuse` 11–14.5°, `root.001` 90° (pitchup only) |
| `dolphin\|yawleft_dolphin` / `yawright_dolphin` | 25 | `fuse` 21–24°, wings 29–36°, bells 21–45° |
| `dolphin\|rollleft_dolphin` / `rollright_dolphin` | 20 / 23 | `root` 24–33°, wings 30–50°, holds up to 70° |
| `dolphin\|speedup_dolphin` / `speeddown_dolphin` | 22 / 19 | wings 87.7° + 0.68 translation (sweep), cases 53–63° |
| `dolphin\|startcharge_dolphin` | 15 | wings translate 3.69, `root.002` translates 1.27, cases 24–58° |

None is a pose; none is bound (no Animator, `clipAnimations []`). They document the artist's intent for each stick axis and are superseded by the C# amplitudes above. **Hazard measured with `Tools/Build/fbx_binary.py`: every take carries four blend-shape `DeformPercent` curves CONSTANT at 100.0 (2 keys) for `mass`, `charge`, `space`, `time`.** Harmless only because nothing plays them; `VesselAnimation.LateUpdate` is the single morph writer precisely to survive such curves (CONTRACT §7). A replacement must export NO shape-key curves (or 0-valued ones) and must not add an Animator.

### 3.5 Flare slots
`FlareEngine/StopFlareEngine` → `materials[3]` (= `N.001`, the bells); `FlareBody(amount)` → `materials[0]` (= `accent.001`, the DOMAIN slot — "body" is a legacy name; the Squirrel's slot 0 is also Domain). `_ColorMultiplier` is a `VesselGraph` property; slot 3 is not remapped to a VesselGraph material (§1.3), so the engine flare is at best inert here.

---

## 4. ELEMENT MORPHS

Discovery: `VesselAnimation.CollectElementShapes` matches shape NAMES (`VesselElementalMorphConfigSO.TryResolveElement`: tail after the last `.`, or a whole token between `_ . - space`; two elements in one name → ignored); weight = `clamp(level, 0, 10)/10 × last-frame weight (100)`; DOTween glide `morphDuration 0.75`, `morphEase 4` (InOutSine) from `Assets/Resources/VesselElementalMorphConfig.asset`; written in `LateUpdate`.

Shipped shapes (probe lines 37–40 as fraction of the 7.582 diag; vessel-space regions from `dolphin_bpy_measure.txt`; the in-editor audit reports the same deltas over a 1.283× larger denominator — Mass 12.056%, Charge 4.314%, Space 3.140%, Time 13.538%, `VESSEL_CONSTRUCTION.md` §4.5):

| shape | verts moved | max travel | what it does (measured) | reads as |
|---|---|---|---|---|
| `mass` | 10,901 (87%) | **1.173 u = 15.47%** REAL | whole-hull INFLATION: mean radial +0.14 outward; bounds grow to x **±3.790** (wing tips +1.146 each), y `[−0.842, 0.975]`, nose z 3.085; touches fuse, both jaws, all six bells, wings | size/volume — but it also stretches the WINGSPAN 43%, which is Space's vocabulary |
| `charge` | 2,329 | 0.420 u = 5.54% REAL | fuse 807 + jaws ≈520 each + wing roots 241 each; mean delta ≈ (0, 0, −0.016); bounds almost unchanged (x 2.655, z 2.850) | a surface detail on jaws/fuselage top; invisible at 20 u |
| `space` | 1,189 | 0.306 u = 4.03% REAL | fuse 869: a KEEL grows DOWNWARD, y-min −0.5675 → **−0.816** within x ±1.49 | neither reach nor presence; reads as a belly |
| `time` | 7,433 | **1.317 u = 17.37%** REAL | all six bells (712 each) + fuse 551 + `jetm.l` 337; mean z −0.062; z-min −2.471 → **−2.738** (nozzles extend 0.27 aft), rear x to ±2.947 (flares out 0.30) | engines lengthen/flare: rate ✓ |

All four are REAL (threshold 0.1%); the Dolphin is the ONLY vessel in the fleet whose rig morphs on all four (`VESSEL_HUD_AND_ELEMENTS.md` lines 53–57).

### 4.1 What each element should express on THIS hull (fleet rule: Space = reach/presence, Time = rate/mobility, Charge = threat/energy, Mass = size/volume), mapped to the shipped abilities (`element_ability_table.py Dolphin`, `DOLPHIN_CRYSTAL_SEEDING.md` §8)

| element | shipped ability (asset is the record) | the thing that grew | proposed geometric change | target travel |
|---|---|---|---|---|
| **Charge** | Echo Sight (RT) + blast capsule THICKNESS 0.75×→1.5×; L5 Pilot Echo | the weapon's girth | thicken BOTH jaw halves (the blast emitter): jaw cross-section 0.22×0.37 → ~0.30×0.50 at the tip, and fatten the throat/coil between them; keep tip z | 0.45–0.60 u (6–8%) |
| **Mass** | Crystal Seeding, recharge ×0.5 at L10; L5 Claimed Seed | volume of ammunition carried | fatten the FUSELAGE belly and the engine-case bodies (radial on `fuse`/`jet*` only); do NOT move the wing tips | 0.75–0.90 u radial (10–12%) |
| **Space** | Echo Obliteration REACH ×2 at L10; L5 Clean Blast | how far the blast reaches | lengthen the jaws FORWARD (tip z 2.827 → ≈3.5) and extend the wing tips (x 2.644 → ≈2.95): reach and presence | 0.60–0.75 u on the jaw tips (8–10%) |
| **Time** | Charge Fill Rate ×1.5 on drift; L5 Drift Ward | speed/mobility | keep the shipped idea and sharpen it: bells lengthen aft (−0.27 → −0.45 z), cases taper, wings SWEEP back ~10° and thin, fuselage sharpens toward the nose | 0.75–0.90 u (10–12%) |

Name the shapes exactly `charge`, `mass`, `space`, `time` (or `blendShape1.mass` etc.); last-frame weight 100; normals exported (`blendShapeNormalImportMode 1`, shipped shapes carry normals — probe `normals=True`).

---

## 5. IDENTITY

- `VesselClassType.Dolphin = 2` (`Assets/_Scripts/Data/Enums/VesselClassType.cs:14`); "DolphinDarts — charge and release", feature-complete playable (`Docs/claude/ABOUT_PROJECT.md:16`; `Docs/ElementalAbilitySystem/FLEET_MAPS.md § Dolphin`).
- **Role**: one weapon in four dimensions — skim prisms to bank Energy (+0.00667/skim, 150 skims to fill, no regen), fly into a crystal to spend it ALL as a cone/capsule blast (Echo Obliteration); passive crystal seeding into the cell's cytoplasm is its own ammunition; a prism ram halves Energy AND Boost and applies the fleet slow (`DOLPHIN_ENERGY_ECONOMY.md` §1). Trail: `VesselPrismController BaseScale (3, 1.5, 8)`, `trailVolume` disabled, no Mass shielding.
- **Input**: two inputs. `R_VesselActionHandler` binds `InputEvents 2 = LeftStickAction` ("lifting a trigger or lifting a thumb") → `DolphinDriftAction + ChargeBoostAction + DriftTrailAction` (the signature drift) and `InputEvents 1 = RightStickAction` → `EchoSightAction` (prefab lines 246–254; `InputEvents.cs:8–9`). On touch: "lift one thumb (Dolphin drift) then the other" (`Docs/BUG_HUNT_HANDOFF_2026-09.md:287`) — a two-thumb vessel whose abilities are the LIFTS.
- **Speed/feel**: `VesselTransformer DefaultThrottleScaler 68`, `Pitch/Yaw/RollScaler 110`, `MaxBoostMultiplier 5`, `VesselStatus boostMultiplier 4`, `holdSpeedWhileDrifting 1`, `driftThrottlePolicy 0 (Locked)`, `restrictedTurnMultiplier 3`; AI `drift 1`, `ram 0`. Mid-fleet (Squirrel camera −17, Sparrow −50, Rhino −120; Dolphin −20 is the fleet's FX reference hull: `widthScale = |followOffset.z|/20`, `VESSEL_TAIL_AND_JETS.md:123`).
- **Camera**: `DolphinCameraSettingsSO followOffset (0, 0, −20)`, no smoothing, `closeCamDistance 8`, `farCamDistance 12`, `controlOverrides 3`; `CameraManager.prefab` FOV 60 (line 318). At 20 u a 60° vertical FOV spans 23.1 u: the 5.29-u wingspan is 23% of frame height (≈250 px at 1080p), the 1.21-u hull height 5% (≈57 px); the pilot looks at the STERN: the six-bell cluster (1.27 u wide × 0.69 u tall) is ≈60×35 px, each bell ≈ 0.16 u ≈ 8 px. Anything under ~0.02 u is sub-pixel; silhouette, nozzle fan, wing sweep and the open jaws are the whole read. The jaw gape is also read on the HUD's jaw icon from the same two angles.
- **Shipped silhouette** (`renders/dolphin_live.png`, clusters §1.2): a slim fuselage (0.65 × 1.21 × 3.45), two long open clamp-like jaw halves ahead of it (each 0.22 wide × 0.37 tall × 2.0 long, from z 0.83 to 2.83, hinged at y ±0.355) with star-shaped cut-outs (the 72-gon polygons) and a coil/throat between them, two long thin swept wings (tip x ±2.644, root chord 0.77, 0.15 thick, at z −0.62..0.15), six engine cases (0.18 × 0.13 × 0.41) fanned about the shared hold origin (0, 0.183, −1.899) at ±45°-ish spacing with six open exhaust bells at z −2.28 — top pair (±0.415, 0.542), middle (±0.575, 0.241), bottom (±0.541, −0.098). Domain colour = slot 0 accent panels, 20% of the surface area; on the legacy per-part art the noses were 62% accent polys (394/640), the cases 38% (175/460), the wings 54% (111/205), the chassis 24% (616/2,547) — the TEAM colour lives mostly on the jaws and wings.
- Docs on its look: the Space ability icon is still placeholder art (`Docs/ElementalAbilitySystem/BACKLOG.md:174`, `ConeBlastIcon-PLACEHOLDER.png`); the HUD's Space band draws the jaw pair (`DOLPHIN_CRYSTAL_SEEDING.md` §8 table). No codex/card-art brief for the hull exists in `Docs/` (grep "dolphin" × codex/card art/silhouette: only the HUD/ability rows).

---

## 6. GAMEPLAY MOUNTS (vessel space, Unity units; all from the prefab tree + swapper table)

| mount | host (parent) | shape / numbers |
|---|---|---|
| **Hull collider** | native GO `Dolphin_Test` under `OrientationHandle` (scale 1, NOT under the FBX), layer 8 | `BoxCollider` trigger, center `(0, 0.0371, −0.7467)`, size `(0.6518, 1.2092, 3.4482)` → x ±0.326, y −0.567..0.642, z −2.471..0.977 = exactly the `fuse` cluster bounds (jaws and wings NOT inside it); `ImpactCollider → VesselImpactor` (prefab line 430–436) |
| **TopNose** | FBX `jaw.u` (GO localScale 0.01, rot `(0, −0.7071, −0.7071, 0)` ⇒ GO-local z = vessel +z) | `BoxCollider` center `(0, 0.2353, 1.5462)` size `(0.2199, 0.3710, 1.9950)` (GO-local; ×1 in vessel units because 0.01×100): the upper jaw, z 0.55..2.54 from the hinge ⇒ vessel 0.83..2.83; `ImpactCollider` |
| **bottomNose** | FBX `jaw.b` | same size, center `(0, −0.2353, 1.5462)`; **BoxCollider only — no `ImpactCollider`** (prefab lines 658–711): the lower jaw registers no impacts. Pre-existing, not a model issue; a replacement should not "fix" it in the FBX |
| **LeftWing / RightWing.001** | FBX `wing.l` / `wing.r` | `BoxCollider` center `(∓1.6799, 0, −0.3322)` size `(1.9274, 0.1499, 0.7696)` ⇒ x 0.716..2.644, y ±0.075, z −0.717..0.053 — the wing cluster is z −0.617..0.153 (collider 0.1 aft of the skin, the legacy 0.1 drag); `ImpactCollider` |
| **Engine case L/R .1/.2/.3** | FBX `jetT.* / jetm.* / jetB.*` (GO scale 0.01) | `BoxCollider` size `(0.1815, 0.1310, 0.4101)`, center `(−0.468..−0.491, ±0.015..0.020, −0.2174)` in the case's local frame (case spans local z `[−0.422, −0.012]`, `VESSEL_TAIL_AND_JETS.md:269`); `ImpactCollider`; layer 8 |
| **Six jets** (`VesselJet.prefab`) | FBX `jetint.l / jetinm.l / jetinb.l / jetint.r / jetinm.r / jetinb.r` (also `mountBone` = the same name, prefab lines 2466–2937) | local scale `(0.006, 0.006, 0.0013)` under the 100× chain = authored puff `(0.6, 0.6, 0.13)`; local pos `(−0.00033, 0.00176, ≈0)` = 0.176 u out along the bell bone. Exhaust mouths measured off the bells' rearmost lip band (`VesselRigSwapper.cs:131–136`): L `(−0.4153, 0.5418, −2.2867)`, `(−0.5752, 0.2409, −2.2824)`, `(−0.5408, −0.0975, −2.2777)`; R mirrored. `widthScale 1.0`. Six mouths ~0.3 apart with a 0.3-wide ribbon each may merge (`TAIL_AND_JETS` §6) |
| **Tail** (`VesselTail.prefab`) | vessel root | `(0, 0, −21)` = `−1.05 × |followOffset.z|` (rule, `TAIL_AND_JETS` §4) |
| **Near-field skimmer** `EnergySkimmer` | `OrientationHandle`, scale 20 | `SphereCollider` trigger r 0.5 ⇒ **10 u** radius at the origin; `Skimmer` (vacuum 80, Scale 20→40 by element 3 Time), `SkimmerImpactor` → `DolphinSkimmerImpactorDataContainer`, `ForcefieldCrackleController` overlay |
| **Skimmer prefab** | `OrientationHandle`, identity | nested `Skimmer.prefab` with `DolphinSkimmerImpactorDataContainer` + 3 prism effects; `VesselPrismController.skimmer` → its `Skimmer` |
| **Blast emitter** | the jaws | gape 4.7636°–23.4287° per jaw; capsule from `DolphinVesselExplosionByCrystalEffect` + `AOEConicExplosion.prefab` (cone height 2400, scale 400→2080) |
| `Passives` | root | `(−0.412, 0.804, 0.768)`, empty |

The two pivots the puppetry seats parts at are also geometry constraints: wings turn about `(0,0,0)` + station `0.5811 z` (authored at `(0,0,0.1)`), cases about `(0, 0.15, −1.7)` (authored `(0, 0.147, −2.047)`); `RiptideAnimation.cs:144–208` and the verifier assert the shipped numbers byte-for-byte (`verify_vessel_rig_puppetry_frames.py` lines 425–500 read both the C# and the prefab).

---

## 7. IN-FLIGHT WORK (`git fetch origin` + `who_works_where.py query`, 2026-10-17)

- **In flight**: `origin/claude/admiring-euler-86o7hv` (2026-10-06, Claude session, "fix(ai): resolve AIPilot's commit drift against the active input device") — diff vs bleeding-edge on `Dolphin.prefab`: **1 insertion**, `+  holdDriftTrigger: 0` (an `AIPilot` field; also touches `Butterfly.prefab` +1, `Squirrel.prefab` +4/−1). No model, material, mount or animation change → a model swap does not collide.
- **Landed on bleeding-edge** touching these files: `#1189 Ys-bleeding-edge` (2026-10-17, merged by Yash: perf pools + morph cache), `#1078` (2026-10-14, Yash/Grok Bot/aradia1), `#1067 claude/serene-edison-lfv24f` (2026-10-13, Shombith). Latest commits: `ed91bbe22 2026-10-17 decide(dolphin): keep the shipped scalar drift hold…`, `8e58821d2 2026-10-06 feat(ai): per-hull AI boost policies`. The FBX itself last changed with the §4.7 repair (commit not re-dated here); the driver's last change is the 2026-10-17 drift decision.
- Local branch vs `origin/bleeding-edge` on prefab/FBX/driver: **empty diff**.
- **Talk to**: Yash (3 merges), Shombith (1), Grok Bot / aradia1 (1 each). Nobody has model work in flight; the "perf pools + morph cache" merge (#1189) is the one to read before touching blend-shape handling.
- A raw scan of every `origin/*` branch for commits on the prefab not in bleeding-edge lists ~150 branches (e.g. `cece/busy-wozniak-pox1vu` 23, `claude/sweet-galileo-m00ltx` 24) — all older than the tool's 30-day window and stale forks, not live work.

---

## 8. CONSTRAINTS FOR A REPLACEMENT (numbered, exact)

1. **File**: write **in place** over `Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx`, keeping its `.meta` (guid `3f1757f09e5b9be478831ec707bdfc51`, `useFileScale 1`, `globalScale 1`, `bakeAxisConversion 0`, `optimizeGameObjects 0`, the three `externalObjects` remaps). Binary FBX 7400 (the repo codec reads/writes it; `fbx_probe` + `assimp info` validate).
2. **Node names to keep (exact, case as shipped)**: armature `dolphin`; skinned-mesh GameObject **`dolohin`**; Mesh/geometry **`Chassis`**; bones `root, fuse, jaw.u, jaw.b, root.001, root.002, winghold.l, winghold.r, wing.l, wing.r, jetholdT.l, jetholdm.l, jetholdB.l, jetholdT.r, jetholdm.r, jetholdB.r, jetT.l, jetm.l, jetB.l, jetT.r, jetm.r, jetB.r, jetint.l, jetinm.l, jetinb.l, jetint.r, jetinm.r, jetinb.r`, with the same parent chain (§1.2) so Unity regenerates the same 20 fileIDs the prefab binds (§2). Do not add a second mesh node or a second armature (`sortHierarchyByName 1`; `ResolvePart` "first occurrence wins").
3. **Bone heads** (vessel space) within ~0.05 u of: `fuse (0,0,0)`, `jaw.u (0, 0.359, 0.932)`, `jaw.b (0, −0.353, 0.932)`, `wing.l/r (∓0.969, 0, −0.114)`, `jetT (±0.270, 0.396, −1.901)`, `jetm (±0.373, 0.201, −1.896)`, `jetB (±0.350, −0.019, −1.892)`, `jetint (±0.359, 0.485, −2.126)`, `jetinm (±0.497, 0.225, −2.122)`, `jetinb (±0.467, −0.067, −2.117)`, holds `(0, 0.183, −1.899)`, `winghold (0, 0, −0.336)`. The collider hosts and jets are children at fixed local offsets, so a moved head moves a collider off the skin. Bone rest ROTATIONS are free (§3.2).
4. **Skin**: one `SkinnedMeshRenderer`, 28 clusters, ≤4 bones/vertex (`maxBonesPerVertex 4`), rigid-ish weights per part (shipped parts are 100% one bone; `jaw.*` 872 each, `wing.*` 294 each, cases 538 each, bells 712 each, `fuse` 2,763) — the puppetry rotates parts rigidly, so soft blends across the jaw hinge or wing root will look like rubber.
5. **Takes to keep**: none are bound. If kept for documentation, keep the names `dolphin|jawcharge_dolphin, pitchdown, pitchup, rollleft, rollright, speeddown, speedup, startcharge, yawleft, yawright` (all `_dolphin`-suffixed, MOTION, 0.833 s). **Export NO blend-shape (`DeformPercent`) curves** — the shipped takes carry all four at a constant 100 (§3.4). Do NOT add an Animator; the poses are owned by the C# amplitudes (§3.3).
6. **Blend shapes**: exactly four, named `mass`, `charge`, `space`, `time` (token rule §4), last-frame weight 100, with normals; travel 6–12% of the 7.58 diagonal per §4.1; morphed geometry must stay clear of the colliders' intent (fuselage collider is a fixed box).
7. **Material slots, in this order, by name**: 0 `accent.001` (DOMAIN, the panels the team colour paints), 1 `BASE.001` (BODY), 2 `windsheild.001` (WINDOW), 3 `N.001` (ENGINE/bell glow). Keep the names so the `.meta` remaps bind; give slot 2 a real cockpit (shipped: 10 polys); add an `externalObjects` remap for `N.001` to a VesselGraph engine material (e.g. the fleet's `LimeVesselMaterial`/engine material) so `FlareEngine` has a `_ColorMultiplier` to drive — that is a `.meta` edit, flag it (`Affects: Yash's perf/morph work`).
8. **Scale/axes to export with**: reproduce the shipped convention exactly — `UnitScaleFactor 1.0`, both root nodes `T (0,0,0) R (−90, 0, 0) S (100,100,100)`, vertices in metres (hull ±2.64 in mesh-local), FBX Y-up, nose along FBX world **+Z** (= mesh-local −Y before the node rotation), starboard at FBX world **−X** (Unity mirrors X). Verify after export with `fbx_probe`: the two root lines must read `R=(-90, 0, 0) S=(100, 100, 100)` and `bounds (-2.64, -2.83, -0.57)..(2.64, 2.47, 0.64)`; a file exported with `UnitScaleFactor 100 / S=1` would shrink every child GameObject (scale 0.01/0.006 compensations) by 100×.
9. **Hull bounds to match** (vessel space): x ±2.644, y −0.5675..0.6417, z −2.4707..2.8270; fuselage within the hull collider x ±0.326, y −0.567..0.642, z −2.471..0.977; jaw tips at z 2.827 (the 2.835 drift-sweep radius and the 3.5 wing lunge were solved from this); wing tips x ±2.644 at z ≈ −0.3; bells' rear lips at z ≈ −2.28 (jet mouths); cases within z −2.245..−1.887.
10. **Forward axis**: Unity +Z = FBX world +Z. Up +Y. The rest pose must be the ship's frame; the puppetry adds no sign flips.
11. **Wiring path — recommended: in-place overwrite** (same guid, same names) = zero prefab edits; the 16 added GameObjects, the SMR reference, `_shipGeometries`, and the four `HullFusionBake.hullMesh` refs all keep binding. Risks: (a) the four `Dolphin_*_HullFusionBake.asset` carry `hullHash 1963952803` / `hullVertexCount 32267` for the OLD mesh → they go stale and must be re-baked in the editor (FrogletTools, CrystalHullFusion) — list it as a hand task; (b) a renamed/added material changes submesh order silently → run `fbx_probe` and confirm `materials=['accent.001','BASE.001','windsheild.001','N.001']`; (c) `optimizeBones 1` strips bones that skin nothing AND have no skinning descendants — keep the hold bones as parents of skinning bones as shipped; (d) `repair_fbx_selfintersecting_polygons.py --check` must pass (no zero-length-edge n-gons); (e) in-editor re-import is the one thing offline cannot prove (self-intersecting-polygon warnings, blend-shape import, fileID regeneration) — a human reimport + `FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs` + `Audit Vessel Construction`. The alternative (new file + editor swap) would need a new `VesselRigSwapper` table (it is authored for `Dolphin_Test → rig`, not rig → rig) and re-points 20 references — strictly more risk, no benefit.

---

## 9. QUALITY BAR

**Keep (what the shipped model gets right)**
- The ONLY fleet rig with four REAL element morphs (mass 15.5%, time 17.4%, charge 5.5%, space 4.0% of diag); the morph audit is green on it (`VESSEL_HUD_AND_ELEMENTS.md`).
- A 28-bone armature authored FOR `RiptideAnimation` (six cases + six bells + two jaws + two wings, each its own rigid cluster), rest-fitted to the legacy hull to 5.5e-5 (§4.3 of VESSEL_CONSTRUCTION), with the six exhaust bells restored as real geometry (the legacy inner nozzles were 0.00095 u, never drawn).
- A strong, legible silhouette at the stern view: six-bell fan + long jaws + thin swept wings; hull proportions 5.29 × 1.21 × 5.30.

**Weak (what to beat)**
- Topology: 17 unwelded loose shells in one mesh, 36 open boundary edges, 443 n-gons up to 72 sides (the star cut-outs), twelve 13-gons that Unity dropped until repaired, twelve zero-area triangles — fan-triangulated n-gons shade badly and the importer's 32,267 split vertices vs 12,595 source vertices say the normals are mostly hard.
- UVs not in 0..1 (u to −0.10, v to −0.49), one layer, no vertex colours: untexturable; everything is flat material colour.
- The WINDOW slot is 10 polygons (0.068 u²): no cockpit read at all.
- Slot 3 (`N.001`) unremapped → embedded material, engine flare inert; `accent.001` authored emissive orange in the FBX (harmless after remap, confusing in a DCC).
- Every take carries constant-100 shape-key curves; the mesh node is misspelt `dolohin`.
- Morph semantics: `mass` stretches the wingspan 43% (reads as Space), `space` grows a keel nobody can see from the chase camera, `charge` moves 0.42 u on surfaces hidden from the stern view (≈10 px at 20 u).
- Six jet mouths 0.3 u apart with 0.3-wide ribbons merge into one sheet (`TAIL_AND_JETS` §6).

**Measured polycount**: 11,113 polys / 25,118 tris / 12,595 verts (32,267 imported). **Target for the replacement**: the hull reads at ≈250 px wide and ≈57 px tall from the shipped camera (§5), bells ≈8 px each; **9,000–16,000 triangles** (all tris/quads, no n-gons, welded manifold shells per part, smooth-shaded with hard edges only where the design wants them), hard ceiling 25,000 (the shipped count, already the fleet's reference FX hull on mobile); spend the budget on the jaws, bells and wing leading edges (the three things the camera and the gameplay look at), not on the fuselage underside or the keel.
