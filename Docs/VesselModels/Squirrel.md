# MODEL DOSSIER — Squirrel

> **Record of the ARTIST file as shipped before 2026-10-17.** On 2026-10-17 the file this dossier measures was
> rebuilt in place by `python3 Tools/Build/vessel_model/build.py squirrel` (spec `Tools/Build/vessel_model/specs/squirrel.py`,
> skill `/vessel-model`): same path and guid, same node, geometry, bone and material names, every bone's rest pose
> reproduced to 0.000 (`rest_compare.py`). Sections 1-7 describe the artist file the spec was written against and
> stay the explanation of WHY the spec keeps each name and number; section 8 is the contract the rebuilt file meets.
> Paths under `<scratchpad>/` were the measuring session's temporary files: the commands beside them regenerate
> every number from the repo.


Measured 2026-10-17 on branch `ccr-d93a6dfb-ugbxng` (== `origin/bleeding-edge` on every file named here: `git diff origin/bleeding-edge --stat` over the prefab, FBX and controller is empty). Every number below names where it was read. Where a doc and an asset disagree, the asset is the record and the disagreement is called out.

Sources:
- `P` = `Assets/_Prefabs/Spacevessels/Squirrel.prefab` (4,209 lines), line numbers as `P:NNNN`
- `F` = `Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx` (2,429,826 bytes, FBX 7400), guid `5db69771d02b4c24ca00998adf318137`
- `M` = that file's `.meta`
- `PROBE` = `python3 Tools/Build/vessel_model/fbx_probe.py "<F>" --takes`, saved at `<scratchpad>/probe/SquirrelVessel_CosmicShoresTest1.txt`
- `GEOM` = bpy (Blender 5.2) analysis `<scratchpad>/squirrel_geom.py`, log at `.../scratchpad/renders/squirrel/geom.log`
- `RENDER` = Cycles renders in `<scratchpad>/renders/squirrel/` (`squirrel_34_nose.png`, `squirrel_34_tail.png`, `squirrel_top.png`, `squirrel_side.png`, `squirrel_nose.png`, `squirrel_shape_{Time,Mass,Space,Charge}.png`)
- `C` = `Assets/_Animations/SquirrelAnimatorController 1.controller` (guid `f076647eb7655744997dda101929faf0`)
- `A` = `Assets/_Scripts/Controller/Animation/MantaAnimationContoller.cs`, `VA` = `.../VesselAnimation.cs`

Coordinate frames used throughout:
- **file** = the FBX's own frame (what `PROBE` and the mesh bounds are in). `GlobalSettings`: `UpAxis=+Z, FrontAxis=+Y, CoordAxis=-X` (`PROBE` line 3: `up=+2 front=+1 coord=-0`).
- **blender** = what bpy shows after import = file rotated 180° about Z (mesh `matrix_world = diag(-1,-1,1)`, `GEOM`): blender (x,y,z) = (−file x, −file y, file z).
- **Unity** = the vessel's frame (+Z nose, +Y up, +X pilot's right). Derived in §1.4: Unity (x,y,z) = (file x, file z, file y).

---

## 1. LIVE MODEL

### 1.1 What the prefab draws and how
- ONE nested model prefab instance: `P:3819 --- !u!1001 &8410834229753696920 PrefabInstance`, `m_SourcePrefab: {fileID: 100100000, guid: 5db69771d02b4c24ca00998adf318137}` (`P:3999`) = `F`. There are **no direct mesh references** in the prefab (every `guid: 5db69771…` line in `P` is inside that instance block or a stripped stub of it, `P:3827–4111`).
- Host: `m_TransformParent: {fileID: 5928382658856804067}` (`P:3825`) = the Transform of the root GameObject `Squirrel` (`P:&6417075533431866457`, tag `Player_Ship`, 31 components). The FBX instance root is the third child of the root Transform (`P:708–730`, `m_Children[2] = 8301242790861813107`, the stripped Transform of the FBX root).
- Other users of the FBX guid in the project (`grep -rl 5db69771… Assets`): the controller `C`, and four `Assets/_SO_Assets/CrystalHullFusion/Squirrel_{Space,Mass,Charge,Time}_HullFusionBake.asset`, each binding `hullMesh: {fileID: -8802436679497846652, guid: 5db69771…}`, `hullVertexCount: 12271`, `hullHash: 4081627646` (§6.6). Nothing else — Thresher and Stoat used to borrow this FBX and no longer do (`Docs/claude/ABOUT_PROJECT.md:26-27`).

### 1.2 Instance transform
Modifications on the FBX root Transform (fileID `-8679921383154817045`, `P:3827–3881`): `m_LocalPosition = (0,0,0)`, `m_LocalRotation = (−0,−0,−0,1)` (identity), `m_LocalEulerAnglesHint = (0,0,0)`, `m_ConstrainProportionsScale = 1`, **no scale modification** → scale (1,1,1). The FBX root GameObject (`919132149155446097`) is renamed `SquirrelShip_CosmicShoresTest1` (`P:3897`). So the model sits exactly on the vessel origin, unrotated, unscaled.

### 1.3 Import scale → hull size in Unity units
- `M`: `globalScale: 1`, `useFileScale: 1`, `useFileUnits: 1`, `bakeAxisConversion: 0`, `meshCompression: 0`, `weldVertices: 1`, `importBlendShapes: 1`, `isReadable: 1`, `optimizeGameObjects: 0`, `animationType: 2` (Generic), `rootMotionBoneName: abone_MAIN_Cockpit`.
- `PROBE`: `UnitScaleFactor=100.0 (original 100.0)`. **The task note ("UnitScaleFactor 100 means cm, divide by 100") is wrong for this file**: FBX `UnitScaleFactor` is centimetres-per-unit, so 100 = metres, and Unity's file scale = UnitScaleFactor × 0.01 = **1.0**. Measured confirmation: the BoxCollider authored in Unity on the mesh node is `m_Size: (4.12, 3.11, 0.59)` (`P:4042`) — 92% of the FBX width 4.496; at ×0.01 the hull would be 0.045 units wide under a 4.12-unit collider, and the skimmer sphere (radius 7.5 u, §6.3) and the 17 u camera (§5.3) would be absurd. **1 FBX unit = 1 Unity unit.**
- Mesh bounds (file frame, `PROBE` + `Tools/Build/measure_vessel_models.py --no-refs --only Squirrel`): `(−2.248, −4.558, −0.6845) .. (2.248, 1.893, 0.770)`, extent **(4.496 W, 6.451 L, 1.455 H)**, diagonal **7.997**.
- **In Unity units (vessel frame): width 4.496 (x ±2.248), length 6.451 (z −4.558 tail tip .. +1.893 nose), height 1.455 (y −0.685 belly .. +0.770 ear tips).** The body without the tail is ≈3.3 long (tail ribs occupy z −1.26 .. −4.56, §1.6).

### 1.4 Which axis is the nose
Geometry (file frame, `GEOM` material centroids converted blender→file by negating x,y):
| slot | centroid (file x,y,z) | y-extent (file) |
|---|---|---|
| Window (cockpit glass) | (0, **+0.591**, +0.130) | −0.32 .. **+1.093** |
| Engine | (0, +0.210, −0.090) | −1.624 .. +1.756 |
| Domain | (0, −0.910, −0.103) | **−4.528** .. +1.433 |
| Body | (0, −0.797, −0.109) | −4.551 .. +1.878 |
Bones (file frame): `bbone_FrontEngineTip.L/R` head y = **+1.539**, `abone_MAIN_Cockpit` y = −0.196, `bbone_WholeTail` y = **−2.399**, `bbone_Tail9Last` y = **−3.956** (`GEOM` bone table, blender y negated).
So the cockpit glass and the FRONT engine pods sit at file **+Y** and the nine-rib tail trails to file **−Y**: **the nose is file +Y** = the FBX `FrontAxis`.

Unity: `VesselTransformer` flies +Z (`Pitch()/Yaw()/Roll()` at `VesselTransformer.cs:921-941` rotate about `transform.right/up/forward`; camera `followOffset (0,0,−17)`; the two `VesselTail`s at `(±4, 0, −12)` on the root, `P:3153-3165`, `P:3330-3345`; the tube is laid along `Vessel.Transform.forward`, `SQUIRREL_TUBE.md`). This hull is the fleet's reference hull and has been flown for two years with no "flies backwards" note anywhere in `Docs/`, so the drawn nose is Unity +Z. A right-handed file into a left-handed engine needs exactly one reflection; swapping file Y and Z is one, so **Unity (x, y, z) = (file x, file z, file y)**: file +Y (nose) → Unity +Z, file +Z (up) → Unity +Y, file X kept — which puts the `.L` bones (file −x, e.g. `bbone_WholeWing.L` at file x = −1.06) on the pilot's LEFT (−X), exactly where the `rollleft` take dips them (§3.5). Same shape as the Dolphin's measured import (`Docs/VESSEL_CONSTRUCTION.md:745-750`: rotations conjugated, local scales preserved). The mesh node's own local frame stays the file frame (its Unity-authored collider reads `x=4.12 width, y=3.11 along the length, z=0.59 thickness`, `P:4042-4043`); the Y-up conversion is carried as a rotation above it. **The one claim to confirm in the editor** (not provable offline): that `bbone_WholeWing.L` lands at negative X in the vessel frame.

### 1.5 Full node hierarchy (`PROBE`, 44 models; T/R in parent space, file frame)
```
a_SquirrelShipMesh_nearfinal [Mesh]  T≈0 R≈0 S=1  geometry=Plane.011  materials=[Domain, Body, Window, Engine]
a_Squirrel_Armature [Null]  T=(0,0,0) R=(71.89, −180, 0) S=1
  bbone_LowerBody            T=(0, −0.0497, −0.3943)   R=(0, −83.78, −90)        skins 122
  bbone_cheek.L / .R         T=(±0.4428, 0.3177, −0.6966) R=(±28.89, 0, −7e-6 / −180)  skins 84 each
  bbone_BackEngineFrame.L    T=(1.522, −0.2885, 0.7601)  R=(−120.1, −101.6, 29.38)  skins 248
    bbone_BackEngineInner.L  T=(0.7288, −0.2877, 0.01916) R≈(0, 0.055, 0.534)       skins 74
  bbone_BackEngineFrame.R    T=(−1.522, −0.2885, 0.7601) R=(−59.89, −78.43, −29.38) skins 248
    bbone_BackEngineInner.R  T=(0.7288, 0.2877, 0.01916)  R≈(0, 0.055, −0.534)      skins 74
  bbone_FrontEngineBase.L/R  T=(±1.818, 0.1238, −0.7694)  R=(±70.45, 62.29, 67.84/112.2)   skins 252 each
  bbone_FrontEngineTip.L/R   T=(±1.943, 0.4579, −1.474)   R=(±82.58, 61.73, 81.47/98.53)   skins 222 each
  bbone_FrontEngineTrim.L/R  T=(±2.176, 0.3063, −1.158)   R=(±80.3, 61.54, 78.88/101.1)    skins 216 each
  abone_MAIN_Cockpit         T=(0, −0.3677, 0.0526)   R=(0, 118.9, −90)         skins 188   (rootMotionBoneName)
  bbone_WholeWing.L/R        T=(±1.06, −0.08134, −0.395) R=(±18.83, −75.71, −109.7/−70.34) skins 0
    bbone_WingPiece1..6.L/R  (six slats per wing; 66/114/80/114/66/42 skinned verts, mirrored)
  bbone_BodyWing             T=(0, 0.1128, −0.3967)   R=(0, −66.58, −90)        skins 130
  bbone_UpperHelmet          T=(0, −0.02388, −0.8402)  R=(0, 117.7, −90)         skins 258
  bbone_LowerHelmet          T=(0, 0.2762, −0.6746)    R=(0, 117.7, −90)         skins 266
  bbone_Ears                 T=(0, −0.08293, −0.7192)  R=(0, −2.03, −90)         skins 477
  bbone_WholeTail            T=(0, −1.047, 2.195)      R=(0, −66.58, −90)        skins 0
    bbone_Tail1First, Tail2, Tail3, Tail4, Tail5, Tail6, Tail7, Tail8, Tail9Last   skins 256 each
```
42 bones = 42 skin clusters; every bone is a direct child of the armature except the six WingPiece per side, the two BackEngineInner and the nine Tail ribs. Bone world positions at rest (file frame = Unity frame after the y↔z swap; from `GEOM`, blender x,y negated): cockpit bone (0, −0.196, 0.22); front pods x ±1.82..±2.18, y +0.76..+1.54; back pods x ±1.52 (Frame) / ±1.11 (Inner), y −0.78 / −1.38; tail ribs on the centreline, y −1.26 (Tail1First) … −3.96 (Tail9Last), z −0.31..−0.62 (the tail hangs below the body line); wing slats x ±0.85..±2.28.

### 1.6 Polycount, materials, UVs, skin
- `PROBE`/`GEOM`: 1 mesh `Plane.011`: **6,901 verts, 6,830 polygons, all quads (0 ngons), 13,660 tris**; normals present; Unity's imported vertex count after seam splitting is **12,271** (`Squirrel_*_HullFusionBake.asset: hullVertexCount`).
- Material slots, `ByPolygon` (`PROBE`): **0 Domain 1,804 · 1 Body 4,400 · 2 Window 186 · 3 Engine 440** polys. Surface areas (`GEOM`, file units²): Domain 8.86, Body 16.99, Window 2.57, Engine 1.80. FBX-authored shading (`PROBE`): Domain Phong diffuse (0.908, 0.671, 0) emissive (0.711, 0.056, 0.065); Body diffuse (0.037, 0.033, 0.046) emissive (1,1,1); Window diffuse (0,0,0) emissive (1,1,1); Engine diffuse (0.00006, 0.803, 0) emissive (1,1,1).
- Which bones carry each slot (`GEOM`, dominant vertex group per polygon): **Domain** = the twelve wing slats (`bbone_WingPiece1..6.L/R`, 112/112/78/64… polys each) plus tail-rib faces; **Body** = Ears 432, LowerHelmet 264, FrontEngineTrim.L/R 216 each, UpperHelmet 208, every Tail rib 192; **Window** = `abone_MAIN_Cockpit` 186 (all of it); **Engine** = BackEngineFrame.L/R 100 each, FrontEngineTip.L/R 76 each, FrontEngineBase.L/R 44 each.
- UVs (`GEOM`): one layer `UVMap`, 27,320 loops, u,v ∈ [0,1] with 1,445 loops (5.3%) outside [0,1]; one UV set only.
- `.meta externalObjects` (`M:7-21`) remaps the FBX materials: Body → `BlueBaseVesselMaterial` (54e78ce1…), Domain → `GreenAccentVesselMaterial` (539a8c65…), Window → `ScreenVesselMaterial` (923d3665…); Engine has no remap. The prefab instance then overrides slots 0, 1, 3 (§2).

---

## 2. WHAT THE PREFAB REFERENCES INSIDE THE FBX

Every `target`/`m_CorrespondingSourceObject` carrying guid `5db69771…` in `P`, with what it must be:

| FBX fileID | Unity type (from the property written) | what it is | what the prefab does with it |
|---|---|---|---|
| `-8679921383154817045` | Transform | FBX **root** transform (stripped `&8301242790861813107`, `P:4103`) | position/rotation/ConstrainProportions (§1.2); child of the vessel root |
| `919132149155446097` | GameObject | FBX root GameObject | `m_Name → SquirrelShip_CosmicShoresTest1` (`P:3897`) |
| `-2358995200592801505` | GameObject | the **mesh node** `a_SquirrelShipMesh_nearfinal` (stripped `&3168954648161338247`, `P:4006`) | `m_Name → a_SquirrelShipMesh` (`P:3887`); **added components**: `ImpactCollider` (`&2470858711757009937`, script `fafcc767…`, `impactorObject → VesselImpactor &2643412773408452782` on the root) and `BoxCollider &6420327366928271` (`P:4024-4044`): non-trigger, `m_Size (4.12, 3.11, 0.59)`, `m_Center (0, −0.03236525, −0.010586868)`; and it is `VesselCustomization._shipGeometries[0]` (`P` component `&7779107667008759293`) — the renderer the domain paint lands on |
| `2144674613060260969` | SkinnedMeshRenderer | the SMR on the mesh node (stripped `&7600425915573274353`, `P:4091`) | `m_RootBone → &8878808177416813356` (= FBX `1120998102324046260`); `m_Materials[0] → RedAccentVesselMaterial` (5e4677e2…), `[1] → BlueBaseOpaqueVesselMaterial` (f5a1f1d4…), `[3] → EngineVesselMaterial` (0c127fff…); slot `[2]` NOT overridden → the `.meta` remap `ScreenVesselMaterial`; `m_BlendShapeWeights[0..3] = 0` (`P:3907-3946`). Referenced by `MantaAnimationContoller.SkinnedMeshRenderer` |
| `5866666021909216657` | Animator | the Animator on the FBX root (stripped `&2725764652992229129`, `P:4000`) | `m_Controller → C`, `m_Enabled 1`, `m_CullingMode 0`, `m_ApplyRootMotion 0` (`P:3947-3966`). Referenced by `MantaAnimationContoller.animator` |
| `-5228419308709608677` | GameObject | **not the root (that one carries the name mod) and not the mesh node** — a node with a layer override: most probably `a_Squirrel_Armature` or the root-bone object (stripped `&4884617224712114563`, `P:4058`) | `m_Layer → 8` = `Ships` (`ProjectSettings/TagManager.asset:33`); **added `BoxCollider &3775377569545099480`** (`P:4066-4083`): `m_IsTrigger 1`, `m_Size (2.79, 3.93, 0.55)`, `m_Center (−0.12, 0, −0.15)`. Its local y spans 3.93 — for a bone frame that is the bone's length axis, and `bbone_LowerBody` runs across the hull width (`GEOM`: head (0,−0.349,0.157) → tail (1.0,−0.349,0.157) in blender), so this is a width-3.93 × length-2.79 × 0.55 body trigger if it sits on that bone. Confirm in the editor; keep that node's name AND orientation, or re-fit the trigger |
| `1120998102324046260` | Transform | the SMR **root bone** (and `m_ConstrainProportionsScale 1`, `P:3902`) — Unity's root bone for a Blender armature is the first bone, `bbone_LowerBody` (first in `PROBE`'s list) | keep the name |
| `-1139028379932300349` | Transform | a transform somebody inspected (`m_ConstrainProportionsScale 1`, `P:3892`) — the armature or mesh transform | harmless |
| `1995807316851797615` | Transform | jet host → `VesselJet` instance `&3003162917154485572` (`P:3970`, `P:1899-1985`) | child added, local `(−0.835, −0.295, −0.024)`, scale `(1.2, 0.6, 0.13)`, `DriftJet.flip 0` |
| `-6731502047082032827` | Transform | jet host → `VesselJet (1)` `&4407056506752009302` (`P:3974`, `P:2157-2245`) | local `(−0.828326, 0.25773132, −0.024306115)`, scale `(1.2, 0.6, 0.13)`, `flip 0` |
| `-2637102738391143170` | Transform | jet host → `VesselJet (2)` `&6084043296942237368` (`P:3978`, `P:3206-3290`) | local `(0.0113, −0.0184, 0.0014)`, scale `(0.6, 0.6, 0.13000003)`, `flip 1` |
| `5223167302631927187` | Transform | jet host → `VesselJet (3)` `&3287304896823774770` (`P:3982`, `P:2050-2130`) | local `(0.01, 0.018, 0.01)`, scale `(0.6, 0.6, 0.13000004)`, `flip 1` |

Which bones host the jets (reasoned, not provable offline — `Docs/VESSEL_TAIL_AND_JETS.md:225` only says "parented to model nodes (authored)"): jets 0/1 are a mirrored pair offset `(−0.83, ∓0.29, −0.02)` from their bones, and the BackEngineFrame→BackEngineInner bone offset is `(0.7288, ∓0.2877, 0.0192)` (`PROBE`), so **VesselJet / VesselJet (1) hang on the back-engine chain (`bbone_BackEngineFrame.L/R` or `bbone_BackEngineInner.L/R`)**, one Inner-length along it; jets 2/3 sit at a bone ORIGIN (offset ≈ 0.02), i.e. **at `bbone_FrontEngineTip.L/R`, the exhaust end of the front pods** (Engine-slot polys are dominated by BackEngineFrame.L/R and FrontEngineTip.L/R, §1.6). The four hosts are four different bones in two L/R pairs, which is what the two `flip` values encode.

Sub-asset fileIDs are **not derivable offline** (`.claude/skills/asset-surgery/SKILL.md:389-396`: MD4 over name/class with every byte order tested, zero hits; re-tested here against the five Squirrel ids above with md4/md5/sha1 over 15 string shapes × 2 encodings — zero hits; and borrowing an id by renaming a node failed on the omni crystal, 2026-10). `fileIdsGeneration: 2` derives them from **type + name**, so the only safe path is the same file and the same node names (§8).

**Exact node names a replacement must keep for these references to bind:** `a_SquirrelShipMesh_nearfinal` (mesh node, with its `SkinnedMeshRenderer` and geometry `Plane.011`'s 4-slot order), `a_Squirrel_Armature`, `bbone_LowerBody` (root bone), the four jet hosts — keep all of `bbone_BackEngineFrame.L`, `bbone_BackEngineFrame.R`, `bbone_BackEngineInner.L`, `bbone_BackEngineInner.R`, `bbone_FrontEngineTip.L`, `bbone_FrontEngineTip.R` with their rest orientation (the jets' authored local offsets/rotations are in bone space) — and `abone_MAIN_Cockpit` (`rootMotionBoneName` in `M`). The root GameObject's FBX name `SquirrelVessel_CosmicShoresTest1` is also hashed into `919132149155446097`.

Things that live on the vessel ROOT and are untouched by a model swap: both `VesselTail`s, the `align skimmer`, `OrientationHandle`, `RearViewCamera`, the HUD, every root component.

---

## 3. ANIMATION

### 3.1 Driver
`MantaAnimationContoller : VesselAnimation` (`A`; component `&5500781737155866732`, `P`): `SkinnedMeshRenderer → stripped SMR`, `SaveNewPositions 1`, `brakeThreshold 0.65`, `lerpAmount 2`, `smallLerpAmount 0.7`, `animator → stripped Animator`, `hasBoost 0`.
- **No code puppetry at all**: `ResolveParts()` is not overridden (base no-op, `VA`), `AssignTransforms()` is `/* NOOP */` (`A`), `Transforms`/`InitialRotations` stay empty, no `CaptureRestRotations`, no `ResolvePart` candidate names. `Idle()` is overridden to drive the Animator only. All motion is the Animator; a replacement needs **no bone names for code** beyond §2. **Since 2026-10-21 one exception:** `DriftEngineSwivel` (on the vessel root, `LateUpdate`, after the Animator) turns the four engine pods onto Course while drifting and finds them BY NAME: `bbone_FrontEngineBase/Trim/Tip.L/.R` and `bbone_BackEngineFrame/Inner.L/.R` (`R_VesselActions/SQUIRREL_DRIFT.md` §12). A replacement keeps those names or re-authors the component's bone lists.
- `PerformShipPuppetry(pitch, yaw, roll, throttle)` (`A`): lerps `current*` toward the inputs at `animationSpeed 3.25`/s, then `animator.SetFloat("Pitch", −currentPitch × 2)`, `("Yaw", +currentYaw × 2)`, `("Roll", +currentRoll × 2)`, `("Throttle", +currentThrottle × 2)`. `Idle()`: lerps to 0 and writes the raw values (no ×2, no sign flip). `"Boost"` is never set (`hasBoost 0`; the controller has no such parameter).
- Inputs (`VA.Update`): idle → `Idle()`; single-stick → `(EasedLeftJoystick.y, EasedLeftJoystick.x, 0, 0)`; two-stick → `(YSum, XSum, YDiff, XDiff)`. The Squirrel is two-stick (`vesselType 6`, scissor throttle, `SQUIRREL_DRIFT.md §1`), so **Pitch=YSum, Yaw=XSum, Roll=YDiff, Throttle=XDiff**. `XDiff = (rightStick.x − leftStick.x + 2)/4 ∈ [0,1], resting at 0.5` (`SQUIRREL_DRIFT.md`), so **`Throttle` rests at 1.0 while flying** (0 only at full brake; 2.0 at full throttle, clamped by the tree) and the `speeddown` pose at Throttle −1 is **unreachable** on this hull's gamepad path.

### 3.2 Controller `C`
Parameters (`C`, `m_AnimatorParameters`): `Blend` (float, default 1), `Pitch`, `Yaw`, `Roll`, `Throttle` (default 0), `x` (default 1), `y` (default 1). One layer `MovementAnimLayer` (`m_DefaultWeight 0` — the base layer, so weight is effectively 1), one state `Blend Tree` (`&-7407348079967575313`: speed 50, `m_TimeParameter: Blend` active, `m_CycleOffsetParameter: Blend`), motion = `Blend Tree &-3065510467884305016`:
- **Top tree** `&-3065510467884305016`: `m_BlendType 2` (SimpleDirectional2D), params `x` / `y`, thresholds manual: child `&1054661598827968095` (Yaw/Pitch tree) at **(1, 0)**; child `&-5535465862024685328` (Roll/Throttle tree) at **(0, 1)**; `idle` clip (`-2403160849616365822`) at **(0, 0)**. **The code never writes `x` or `y`**, so the point sits at their defaults (1, 1), exactly between the two children: each subtree is mixed at ≈½ weight. Every pose the pilot sees is therefore HALF its authored deflection, blended with the other tree (which at cruise is the `speedup` pose, §3.1).
- **Yaw/Pitch tree** `&1054661598827968095`: type 2, `Yaw` = x, `Pitch` = y, automatic thresholds: `pitchup` (`3850382461062512866`) **(0, 1)** · `pitchdown` (`6027476600366338588`) **(0, −1)** · `yawright` (`-2194766280897318139`) **(1, 0)** · `yawleft` (`7868478854632894093`) **(−1, 0)** · `idle` **(0, 0)**.
- **Roll/Throttle tree** `&-5535465862024685328`: type 2, `Roll` = x, `Throttle` = y: `speedup` (`4102268083176662213`) **(0, 1)** · `speeddown` (`-5886390736306840818`) **(0, −1)** · `rollleft` (`-8583781936895361006`) **(1, 0)** · `rollright` (`-3465231570906512904`) **(−1, 0)** · `idle` **(0, 0)**.
- Orphan trees not reachable from the state but still serialized in `C`: `&-8976423722360036457`, `&-6502134309559327549`, `&-1029098711150410461` (clips from `mantis_shapekey_with_animations.fbx`, guid 7c324ef5…), `&-1900624049200794090 'urchan fly'`, `&1107033417084343733 'throtle'`, `&1887696347075263162 'roll'` (clips from `urchan_shapekey_with_animations.fbx`, guid 7f9bbc7b…), plus six empty `BlendTree`s. They make the controller depend on two other model files for nothing (`Docs/VESSEL_CONSTRUCTION.md:1146` counts this controller among the urchin FBX's nine users).

### 3.3 `.meta` clipAnimations (`M:49-283`) — exact take names and ids
All nine: `name == takeName`, `firstFrame 0, lastFrame 1`, `loop 0`, `loopTime 0`, `wrapMode 0`, `keepOriginalPositionY 1`, full bodyMask, `maskType 3`:
| take | internalID |
|---|---|
| `a_Squirrel_Armature\|squirrelanim_idle` | `-2403160849616365822` |
| `a_Squirrel_Armature\|squirrelanim_pitchdown` | `6027476600366338588` |
| `a_Squirrel_Armature\|squirrelanim_pitchup` | `3850382461062512866` |
| `a_Squirrel_Armature\|squirrelanim_rollleft` | `-8583781936895361006` |
| `a_Squirrel_Armature\|squirrelanim_rollright` | `-3465231570906512904` |
| `a_Squirrel_Armature\|squirrelanim_speeddown` | `-5886390736306840818` |
| `a_Squirrel_Armature\|squirrelanim_speedup` | `4102268083176662213` |
| `a_Squirrel_Armature\|squirrelanim_yawleft` | `7868478854632894093` |
| `a_Squirrel_Armature\|squirrelanim_yawright` | `-2194766280897318139` |
The controller's `m_Motion` fileIDs are exactly these internalIDs, so the binding is take-name → pinned internalID → controller. A replacement that keeps the nine take names keeps the controller binding without touching `C` or `M` (`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md:79-83`).

### 3.4 Pose or motion (`PROBE --takes`)
Every take is **0.000 s, one key per curve** → **nine single-frame POSES, no motion**. Curves on 43 nodes each; nodes displaced from rest: `idle` **0** (it IS the rest pose), `pitchdown` 39, `pitchup` 37, `rollleft` 27, `rollright` 27, `speeddown` 41, `speedup` 29, `yawleft` 27, `yawright` 27. **Zero blend-shape curves in every take** (load-bearing: `VA.LateUpdate` is the single writer of shape weights; a re-export carrying even constant-zero shape curves is the trap `CONTRACT.md §7` records).

### 3.5 What each pose deflects, in the Unity frame (`GEOM`, blender→Unity: x same, y = blender z, z = −blender y; rest-relative world deltas of bone heads, tilt = angle between rest and posed bone axis)
| take | tail (`bbone_WholeTail` head / `Tail9Last` head) | front pods (`FrontEngineTip`) | wings (`WholeWing.L` / `.R`) | cockpit / crest | reads as |
|---|---|---|---|---|---|
| pitchup | down 1.13 & fwd 0.52 / down 1.42, fwd 0.58 | up 0.58, back 0.20, tilt 16° | WingPiece6 (rear slat) down 0.71 | cockpit down 0.34; helmet up 0.14, ears up 0.07 | **nose-up attitude** (tail drops, pods rise) |
| pitchdown | up 1.81 & fwd 0.50 / up 0.55, fwd 0.78 | down 0.62, tilt 20° | rear slat up 1.18 | cockpit up 0.40, helmet down 0.30 | **nose-down** |
| rollleft | tail tilt 45°, `Tail9Last` 110° (curls), x −0.44 | L pod down 1.17, R pod up 1.25, tilt 39° | **L wing down 0.68, R wing up 0.64**, tilt 38° | body bones tilt 37–38° | **bank LEFT** (left wing dips) |
| rollright | mirror of rollleft | mirror | **L up 0.64, R down 0.68** | mirror | bank right |
| yawleft | tail head x **+2.21** (pilot's right), fwd 0.93, tilt 28° | L pod back 2.39 (z −2.39), R pod x +2.08 | L wing x −0.71, R wing x +0.56, tilt 66° | cockpit x +0.55, fwd 0.34, tilt 63° | a **left drift**: body slides and the tail whips OUT to the right of the turn |
| yawright | mirror (tail x −2.21) | mirror | mirror | mirror | right drift |
| speedup | back 0.18 / `Tail9Last` back 1.21, up 0.11 | back 0.39 | back 0.34; `BackEngineInner` back 1.34 | ears back 0.20 | everything streaks **backward** — the speed pose |
| speeddown | fwd 0.63 / **fwd 1.94** | fwd 0.29 | `BackEngineInner` tilt **70°** (pods swing out), wings tilt 5–8°, rear slat fwd | — | air-brake |
Idle vs rest: the `idle` take moves the tail +0.53 up and the front pods −0.21 down relative to the bind pose (`GEOM` ACTION idle), so the shipped resting silhouette is the `idle` pose, not the bind pose; `m_RootBone`'s pose is rest.

**Direction convention, closed:** `VesselTransformer.Pitch()` rotates about `transform.right` by `+YSum` (`:921-927`), and in Unity `Euler(90,0,0)·forward = down`, so YSum > 0 = nose DOWN; the driver writes `Pitch = −YSum × 2`, so nose-down input reaches the **`pitchdown`** child at (0,−1) — and that take is the nose-down pose measured above. **Yes, `pitchup` pitches the nose UP in Unity** (the sign flip in `A` is what makes the take names honest). Yaw: `+XSum` about `transform.up` = nose RIGHT (`Euler(0,90,0)·forward = right`), `Yaw = +XSum × 2` → `yawright` at (1,0) ✓. Roll: `+YDiff` about `transform.forward` tilts the up vector toward −X (`Euler(0,0,90)·up = left`) = left wing down = bank LEFT, `Roll = +YDiff × 2` → `rollleft` at (1,0), which dips the `.L` wing ✓ (this is the measurement behind the `.L = pilot's left` claim in §1.4).

### 3.6 FlareEngine / FlareBody slots (`VA:467-487`)
`FlareEngine()` → `SkinnedMeshRenderer.materials[3].SetFloat("_ColorMultiplier", 5)` = **slot 3 = Engine** (`EngineVesselMaterial`, the pod mouths + back frames, 440 polys). `FlareBody()` → `materials[0]` = **slot 0 = Domain** (1,804 polys of wing slats and rib faces, wearing the domain material at runtime). Both go through `.materials` (instancing); keep slot 0 = the domain surface and slot 3 = the engine glow surface or these flashes land on the wrong faces.

---

## 4. ELEMENT MORPHS

### 4.1 Shipped shapes (`PROBE`; all four `REAL`, `fullW=100.0`, normals=True)
| shape | indexed verts | moved | maxD | % of diag 7.997 | sumD |
|---|---|---|---|---|---|
| `Time` | 4,199 | 3,614 | 0.5096 | **6.37%** | 217.5 |
| `Mass` | 5,427 | 4,727 | 0.1969 | **2.46%** | 267.3 |
| `Space` | 5,938 | 5,518 | 0.3689 | **4.61%** | 841.8 |
| `Charge` | 3,680 | 1,419 | 0.8849 | **11.07%** | 168.2 |
Names are the bare element tokens, which `VesselElementalMorphConfigSO.TryResolveElement` matches exactly (`:60-98`, case-insensitive tail token); `CollectElementShapes` (`VA:433-461`) reads the last frame weight (100) as the extreme; the prefab pins all four `m_BlendShapeWeights` to 0 (`P:3927-3946`); the audit's `MinShapeTravelFraction` sits inside the measured 2.46–17.94% real gap (`CONTRACT.md §7`), so **Mass at 2.46% is this fleet's floor**.

### 4.2 What each shape does today (`GEOM` per-region travel, extents; `RENDER squirrel_shape_*.png`)
- **Time** (`squirrel_shape_Time.png`): mean displacement **backward** 0.043 (blender +y), regions FrontEngineBase.L/R 26.4 each, every tail rib ≈11.6; hull length 6.451 → **6.568** (tail ribs reach back +0.12); the ribs sharpen into swept chevrons and the front pods stretch. Reads: *sharper, faster*.
- **Mass** (`squirrel_shape_Mass.png`): mean |Δz| 0.050 (thickening), regions Tail9Last 17.0, Tail1First 16.8, Tail2 15.1, Tail8 14.9, BackEngineFrame 13.9; height 1.455 → **1.509**; ribs, wing slats and panels fatten into rounded tubes. Reads: *heavier* — but at 0.05 mean it barely registers at 17 u.
- **Space** (`squirrel_shape_Space.png`): mean |Δx| **0.146** (lateral), regions FrontEngineBase 70.9 / Tip 62.4 / Trim 61.2 per side, tail ribs ≈35; width 4.496 → **5.018** (+11.6%); the front pods and wing slats spread outward and the tail ribs open into loops/rings. Reads: *wider reach*.
- **Charge** (`squirrel_shape_Charge.png`): mean displacement backward 0.082, regions **Ears 36.7**, FrontEngineTrim 19.8 each, BodyWing 18.1, UpperHelmet 14.3; length → **6.838** (+0.39), height → **1.595** (+0.14); the ear crest grows into two long spikes, the pod tips and wing tips grow spikes. Reads: *armed / threatening*. Only 1,262–1,419 verts move — it is a crest morph, not a hull morph.

### 4.3 What each element SHOULD say on this hull (fleet rule × this hull's abilities)
`python3 Tools/Build/element_ability_table.py Squirrel` (4/4 wired, 0 disagreements) and `SQUIRREL_ELEMENT_RECUT.md`: **Charge = Crystal Joust** (passive; petals stolen per overtake ×1 → ×2.5; L5 Shepherd) · **Mass = Boost Ring** (RT; ring cooldown ×1 → ×0.5; L5 Twin Rings; "Mass creates mass") · **Space = Steal** (passive; skimmer sphere 15 → 30 u; L5 Iron Grip) · **Time = Skimming** (passive; skim energy ×1 → ×2; L5 Live Wire). Fleet rule: Space = reach/presence, Time = rate/mobility, Charge = threat/energy, Mass = size/volume. The shipped shapes already map onto this; the replacement keeps the semantics and fixes the magnitudes:
| element | ability it tells the pilot about | geometric change on the replacement | target travel (diag ≈ 8 → 3–15% = 0.24–1.2 u) |
|---|---|---|---|
| **Charge** | the joust's bite (petals stolen) | the ear crest and the two front-pod tips grow into forward-raked blades (+0.7–0.9 along their axis), wing-slat tips sharpen; keep it a crest morph but ALSO harden the canopy frame edge so the nose reads armed from behind | max **0.8–0.9** (10–11%), ≥2,000 verts moving |
| **Mass** | Boost Ring (lays danger prisms) + heavy trail | inflate the body, the nine tail ribs and the back-engine frames along their normals (+0.25–0.35), drop the belly 0.15; the ship must look *heavier from behind*, which is where its pilot sees it | max **0.32–0.40** (4–5%) — above today's 0.197, which is the fleet floor |
| **Space** | skimmer reach 15 → 30 u (the steal) | spread the front pods and the six wing slats outward (±0.35–0.45 x each side), open the tail ribs into wider rings (+0.3 radius) | max **0.40–0.45** (5–5.5%), width 4.50 → ≈5.3 |
| **Time** | skimming rate / speed | sweep the tail ribs back and thin them (+0.45–0.55 z-aft), elongate the front pods (+0.3), pull the canopy lower | max **0.50–0.55** (6–7%), length 6.45 → ≈6.9 |
Authoring rules (all measured as load-bearing): exact names `Time`, `Mass`, `Space`, `Charge` (or any name whose last `.`-token / sole `_-. `-delimited token is one of them); one frame at weight 100; blend-shape NORMALS authored (`M: blendShapeNormalImportMode 1`; `asset-surgery SKILL.md §4.8d`); **no blend-shape animation curves in any take**; shapes on the ONE skinned mesh under `a_SquirrelShipMesh_nearfinal`.

---

## 5. IDENTITY

- **Role** (`Docs/claude/ABOUT_PROJECT.md:20`): vesselType **6**, "Racing/drift — vaporwave arcade racer, tube-riding along player-generated trails (F-Zero / Redout feel)". Class copy (`Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset:16-19`): *"Get in your flow and stay there. Hard decisions are for other classes. You're here to bounce around and ride your speed tubes, leaving a wake of creation and destruction."* `PrimaryElement: 3`.
- **Controls**: two-stick (scissor throttle `XDiff`, `SQUIRREL_DRIFT.md §1`); LT = analog drift (`singleTriggerDrift 1`, `vectorFlightModel 1`, `driftOvershootCeiling 1.25`, `P` VesselTransformer `&7686683633640807837`); RT = Tube / Boost Ring (`SquirrelTubeAction.asset: radius 8, forwardOffset 100, leadSeconds −1.5`); touch = right-thumb lift drifts. Flight numbers (`P`): `DefaultThrottleScaler 60`, `PitchScaler 120`, `YawScaler 120`, `RollScaler 130`, `MaxBoostMultiplier 5`, `BoostDecayRate 0.3`, `restrictedTurnMultiplier 3`, `touchNoseResponse 9`. Speed class: 60 u/s throttle scaler, the fleet's reference racer (`VESSEL_TAIL_AND_JETS.md:214-216`: "it is the reference hull").
- **What it does to prisms**: lays a trail (drifting lays SHIELDED prisms at every level — `driftShieldsTrail`, `VESSEL_HUD_AND_ELEMENTS.md:192`); the Boost Ring lays a ring of DANGER prisms 8 u in radius ahead (`BoostRingBuilder`); the skimmer STEALS opposing prisms within 15–30 u (`SkimmerStealPrismEffect`); an omni crystal becomes a ring of eight shielded prisms just ahead of the nose (`SQUIRREL_CRYSTAL_MORPH.md`, `SquirrelCrystalMorphByCrystal.asset` in `omniCrystalRetirement`); the joust strips petals from overtaken pilots. PvP is petals only.
- **Shipped silhouette** (`RENDER`, §1.5-1.6): a thin wedge body (1.45 tall on 4.5 wide) with a teardrop glass canopy (Window) at the nose; a twin-fin "ear" crest behind the canopy (477 verts, the biggest single part); cheek fairings (`bbone_cheek.L/R`); two forward-pointing engine pods on the wing-tips (`FrontEngineBase/Tip/Trim`, green Engine mouths) and two aft pods (`BackEngineFrame/Inner`); each wing is **six separate floating slats** (`WingPiece1..6`) wearing the Domain colour; and a **nine-rib segmented tail** (`Tail1First..Tail9Last`, 256 verts each, chevron ribs with Domain faces) trailing 2.7 u behind the body, hanging 0.3–0.6 below the body line — the squirrel's bushy tail and the "chevron tail" of the fleet brief. Vaporwave: flat emissive panels, no texture detail.
- **Camera** (`Assets/_SO_Assets/Camera/SquirrelCameraSettingsSO.asset`): `followOffset (0, 0, −17)`, `closeCamDistance 17`, `farCamDistance 17`, `followSmoothTime 0.2`, `rotationSmoothTime 5`, `nearClip 0.3`, `farClip 12000`. At 17 u a 6.45 u hull subtends ≈21° vertically-equivalent: the rib stack, the crest, the pod mouths and the slat gaps all read; 0.05 u details do not; the pilot sees the hull from BEHIND and slightly above, so the tail, the back pods and the crest are the identity — the canopy is mostly hidden.
- **Domain colour slot**: slot **0 `Domain`** — `VesselCustomization` (`P &7779107667008759293`: `_shipGeometries [mesh node]`, `_domainMaterialSlot 1`, `_domainReplacesMaterials []`) → `HasDomainMaterialIdentities()` false → `ShipHelper.ApplyShipMaterial(material, geometries, 1)` which for a **SkinnedMeshRenderer writes `materials[0]` regardless of the slot argument** (`VesselHelper.cs:164-169`); the `_domainMaterialSlot 1` only applies to MeshRenderers. The FBX's slot 0 is `Domain` ✓, the instance pre-paints it `RedAccentVesselMaterial`, the `.meta` remap says `GreenAccentVesselMaterial`, the FBX says orange — all overwritten at spawn.
- **Art references**: class icons `IconActive 5d8f022f…`, `IconInactive c7a15d77…` (`SO_Class_Squirrel.asset:27-28`); `Assets/_Graphics/Design Assests/Hangar/Squirrel_Hangar_Preview.png`; `Assets/_Graphics/Codex/tool_vessel-changer__squirrel.png`; `Assets/_Graphics/Silhouettes/Squirrel/aSquirrel.png`; HUD `SquirrelHUDVariant` / `SquirrelVesselHUDView` (`VESSEL_HUD_AND_ELEMENTS.md:192`); legacy `{LEGACY}/Minigames/SQUIRREL.png`.

---

## 6. GAMEPLAY MOUNTS (Unity units; positions in the frame named)

1. **Jets ×4** — `VesselJet.prefab` (guid 55ceca57…) + `DriftJet` (script 4fa735e2…, aims the plume along the drift course — Squirrel-only, `VESSEL_TAIL_AND_JETS.md:491-492`), `vesselStatus → &6441696348568773771`. Parent = a stripped bone Transform inside the model instance (§2), so **no `m_Children` entry exists or can** (`VESSEL_CONSTRUCTION.md §3`). Authored local TRS (bone space): `VesselJet` (−0.835, −0.295, −0.024) q(−0.4016, 0.4815, −0.5963, 0.5013) s(1.2, 0.6, 0.13) flip 0 · `VesselJet (1)` (−0.828, 0.258, −0.024) q(−0.4815, 0.4016, −0.5013, 0.5963) s(1.2, 0.6, 0.13) flip 0 · `VesselJet (2)` (0.0113, −0.0184, 0.0014) q(−0.7647, −0.0423, 0.6407, −0.0542) s(0.6, 0.6, 0.13) flip 1 · `VesselJet (3)` (0.01, 0.018, 0.01) q(0.0423, 0.7647, 0.0542, −0.6407) s(0.6, 0.6, 0.13) flip 1. The (0.6, 0.6, 0.13) plume scale is the **fleet reference** (`VESSEL_TAIL_AND_JETS.md:137-140`); width× 0.85 (`:225`). World mount points on the shipped hull (bone heads, Unity frame): front pod exhausts at ≈ (±1.94, +0.23, +1.54) and back pods at ≈ (±1.11..±1.52, −0.47..−0.19, −1.38..−0.78).
2. **Tails ×2** — `VesselTail.prefab` (guid ef25530d…) on the vessel ROOT at **(4, 0, −12)** and **(−4, 0, −12)**, identity rotation (`P:3133-3196`, `P:3318-3390`); the one authored pair in the fleet, "left alone because it works" (`VESSEL_TAIL_AND_JETS.md:214-216`). Not inside the model.
3. **Skimmer** — `align skimmer` = `Skimmer.prefab` (guid 6540c785…) under `OrientationHandle` (`&1878660672777590133`, at the origin) at (0,0,0), `m_LocalScale 15` → `SphereCollider r 0.5` = **7.5 u radius** sphere centred on the vessel (`P:3400-3800`, `Skimmer.prefab:51-70`). `VesselStatus._nearFieldSkimmer → &708149374973573223`, `_farFieldSkimmer: {fileID: 0}`. Space scales its reach 15 → 30 (element table).
4. **Rear camera** — `PipCamera.prefab` as `RearViewCamera` under `OrientationHandle` at (0, 0, **25**) rotated (0, 180, 0), `m_IsActive 0` (`P:4121-4197`).
5. **Colliders** — (a) mesh node `a_SquirrelShipMesh`: `BoxCollider` **(4.12, 3.11, 0.59)** centre (0, −0.0324, −0.0106) in the mesh node's local (= file) frame → covers the body from z −1.59 to +1.52, x ±2.06, y −0.33..+0.28 (excludes the tail, the ears and the belly); `ImpactCollider` routes hits to `VesselImpactor` (`SquirrelImpactorDataContainer.asset`, `networkVesselImpactor`). (b) layer-8 node (§2): trigger `BoxCollider` **(2.79, 3.93, 0.55)** centre (−0.12, 0, −0.15). Root `Rigidbody` kinematic, `m_Constraints 126`.
6. **Guns/bays**: none. The tube/Boost Ring is laid in world space 100 u ahead (`SquirrelTubeAction.asset`), not from a mount. **Crystal-hull fusion**: four bakes (§1.1) bind the mesh sub-asset and its content hash; a new mesh makes them **STALE** (`CrystalHullFusionBaker.cs:219`: "re-exported … with the same vertex count" → `BakeState.Stale`; runtime falls back to solving on a worker thread with a `WarnOnce("stale:…")`, `CrystalHullFusion.cs:32,632`) → re-run **FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions** after any swap.

---

## 7. IN-FLIGHT WORK (`git fetch origin`; `python3 Tools/Build/who_works_where.py query <prefab> <fbx>`, 2026-10-17)

In flight (unmerged, touching `Assets/_Prefabs/Spacevessels`):
- `claude/admiring-euler-86o7hv` (2026-10-06, 1 commit "fix(ai): resolve AIPilot's commit drift against the active input device"): `git diff origin/bleeding-edge...origin/claude/admiring-euler-86o7hv --stat -- Squirrel.prefab` → **+3 −1**: adds `touchNoseResponse: 9`, flips `drift: 0 → 1`, adds `holdDriftTrigger: 1` (AI/transformer fields on the root). Also Butterfly/Dolphin +1 each. **No model, meta or controller change.**
- `claude/android-performance-stripped-dap5z2` (2026-10-02, 12 commits, "fix(strip): toybox filter names the Wander toy; With Ark stays off on Android"): **+6 −5** on `Squirrel.prefab`: adds `touchNoseResponse: 9` and blanks the FMOD `event:/SFX/Oneshots/Gameplay sfx/Skim` reference (GUID → 0) on an audio component. **No model change.**
Neither branch touches `F`, `M` or `C` (diff empty for those paths). A model swap collides with nothing in flight; both branches' prefab hunks are on root components, so a swap that only edits the FBX instance block merges cleanly.
Landed on bleeding-edge in the last 30 days: #1078 `Ys-bleeding-edge` (merged by **Yash**; commits by Yash, Grok Bot, aradia1), #1067 `claude/serene-edison-lfv24f` (merged by **Shombith**), 70bd3dfd6 (touch controls). Last commits: `F` f7ce592bb 2026-10-04; `P` 312f2a736 2026-10-13. **Talk to: Yash (2), Grok Bot (1), aradia1 (1), Shombith (1)** before replacing art they play-test on.

---

## 8. CONSTRAINTS FOR A REPLACEMENT (numbered, exact)

1. **File**: write **in place** over `Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx`, same guid `5db69771d02b4c24ca00998adf318137`; do not touch `M` except (if needed) nothing — the nine `clipAnimations` entries and `externalObjects` remaps stay valid when the names below are kept. FBX 7400 binary, `UnitScaleFactor 100` (metres = Unity units), `UpAxis +Z, FrontAxis +Y, CoordAxis −X` exactly as shipped (change the axes and the mesh-node collider, the trigger collider and the four bone-space jet offsets all rotate).
2. **Frame/size**: nose along file **+Y**, up file **+Z**, pilot's left at file **−X**. Hull bounds to match within ±5%: width **4.50** (x ±2.25), length **6.45** (nose at +1.89, tail tip at −4.56), height **1.46** (−0.68..+0.77); body (non-tail) ≈3.3 long so the (4.12, 3.11, 0.59) collider still fits; tail ribs occupying −1.26..−4.56. Diagonal ≈8.0.
3. **Node names to keep (exact, case-sensitive)**: root `SquirrelVessel_CosmicShoresTest1`; mesh node `a_SquirrelShipMesh_nearfinal` with ONE skinned mesh carrying four slots and four shapes; armature `a_Squirrel_Armature`; root bone `bbone_LowerBody` first in the armature; `abone_MAIN_Cockpit`; the jet hosts `bbone_BackEngineFrame.L/.R`, `bbone_BackEngineInner.L/.R`, `bbone_FrontEngineTip.L/.R` — at the same rest world positions (±0.1) and rest orientations as §1.5 (jet offsets/rotations are in bone space). Keeping all 42 bone names (`PROBE` list) is cheapest and keeps every take binding at full fidelity.
4. **Take names to keep** (nine, all single-frame POSES, 1 key per curve, 0 blend-shape curves): `a_Squirrel_Armature|squirrelanim_idle` (rest), `…_pitchup` (tail down ≈1.1–1.4, front pods up ≈0.6), `…_pitchdown` (tail up ≈1.8, pods down ≈0.6), `…_rollleft` (`.L` wing down ≈0.7, `.R` up ≈0.6, body tilt ≈38°), `…_rollright` (mirror), `…_yawleft` (tail whips to +X ≈2.2, nose x +0.5, body tilt ≈63°), `…_yawright` (mirror), `…_speedup` (parts streak aft 0.3–1.3), `…_speeddown` (tail forward ≈1.9, back pods swing out ≈70°). Because the top blend tree halves every pose (§3.2), author poses at least this big; the names map Blender action → FBX take as `<armature>|<action>`.
5. **Slot order and meaning**: `0 Domain` (the domain-painted surface: wing slats + rib faces; also `FlareBody`), `1 Body` (dark hull), `2 Window` (canopy glass; keep the `.meta` remap name `Window`), `3 Engine` (pod mouths; `FlareEngine`). Material NAMES in the FBX must stay `Domain`, `Body`, `Window` so `externalObjects` keeps remapping; polygon shares near 26/64/3/7%.
6. **Shapes**: exactly `Time`, `Mass`, `Space`, `Charge`, weight 0→100, with normals, magnitudes per §4.3 (Mass ≥ 4% of the diagonal, Charge ≤ 12%), no shape curves in any take.
7. **Skin**: one skinned mesh, ≤4 bones/vertex (`maxBonesPerVertex 4`), `optimizeBones 1` — every bone must skin ≥1 vertex or Unity strips it (today `bbone_WholeWing.L/R` and `bbone_WholeTail` skin 0 verts and survive only as parents; keep them as parents of skinned children).
8. **After writing**: `assimp info` before/after (submesh order, 4 materials, 9 animations, bounds); `python3 Tools/Build/vessel_model/fbx_probe.py <F> --takes`; `measure_vessel_models.py --only Squirrel`; in-editor: `FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs`, `Audit Vessel Tails and Jets` (the four jets must still resolve), `Audit Vessel Construction`, then **`FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions`** (§6.6), and a flight to confirm `.L` lands at −X and the jets sit in the pod mouths.
9. **Wiring path: in-place overwrite (same guid, same names)** — the prefab needs **no edit**; the only alternative (new file + editor re-point) re-hashes every sub-asset id, dangles the SMR/Animator/root-bone/jet-host/layer-8 stubs and both colliders, and has to be redone by hand in the editor (`asset-surgery SKILL.md:3003-3011`). **Risk of in-place**: Unity derives ids from type+name, so one renamed or re-typed node silently detaches whatever referenced it (a jet falls back to the prefab origin with a loud `ResolveMountBone`-style error only for name-mounted jets — these four are id-mounted and would just float at the vessel origin); and the hull-fusion bakes go stale until re-baked.

---

## 9. QUALITY BAR

**Keep (measured strengths):** four material slots in the right order with ~26% of the surface on the domain slot, so the team colour is big and legible; a complete 42-bone rig whose nine poses are full-body (27–41 bones move per pose) and clean (no shape curves); four REAL element shapes whose semantics already match the fleet rule and the 2026-09-24 ability re-cut (Charge = crest blades, Mass = fatten, Space = spread, Time = sweep); all-quad topology (6,830 quads, 0 ngons); one UV set; one mesh, one armature, no vestige nodes; a silhouette that reads at 17 u from behind (rib stack, crest, four pod mouths).

**Weak (measured):**
- The tail is **nine disconnected ribs** and each wing **six floating slats**; the panels are open shells, not a watertight hull — it reads as chips in orbit rather than a body (`RENDER 34_tail`, `top`).
- Takes are **poses only**: no idle breathing, no tail lag; the controller halves every pose (x,y unset) and mixes it with `speedup` (Throttle rests at 1.0), and `speeddown` is unreachable (§3.1-3.2).
- **Mass** moves the hull 2.46% of its diagonal — the fleet floor — and **Charge** moves only 1,262 verts (ears); from behind at 17 u, Mass is invisible.
- The jets do not sit in anything that looks like an exhaust: jets 0/1 are 0.83 u off their bone, and the "pods" are open frames.
- 5.3% of UV loops leave [0,1] with a single UV set; the shipped materials are flat so it costs nothing today, but a textured replacement would tile.
- The file is a **byte-level splice** of two exports (`VESSEL_HUD_AND_ELEMENTS.md:70-84`) with a `nearfinal` mesh name; `C` carries orphan trees into two other FBXs; the trigger collider's host node cannot be named offline.
- **Polycount**: 13,660 tris / 6,901 verts (12,271 after seam splitting) for a hull whose pilot sees it at 17 u. Target for the replacement: **8,000–12,000 tris** (≤ the shipped 13.7k), spent on a watertight body, a real canopy frame, pod mouths that house the jets, and ribs that connect to a spine; shape-key travel per §4.3.
