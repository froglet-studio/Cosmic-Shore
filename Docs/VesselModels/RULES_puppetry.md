# RULES_puppetry — the puppetry and take specification per vessel (measured 2026-10-17, branch `ccr-d93a6dfb-ugbxng`)

> Measured rules (puppetry and takes), written by an independent reader on 2026-10-17 from the artist files and the prefabs as they
> were before the `/vessel-model` rebuild. They describe what TODAY's prefabs, metas and drivers bind, so an in-place
> replacement lands without the editor; they are not the convention. Where a rule here says "keep X as shipped" and
> `.claude/skills/vessel-model/references/FLEET_CONVENTION.md` says otherwise, the convention is the intention and the
> rule is a legacy binding with a migration step there (Garrett, 2026-10-17: the shipped vessels are works in progress).
> Paths under `<scratchpad>/` were the reader's temporary files; the commands beside them regenerate the numbers.


Every number below was measured on this tree with the command named next to it. Where a doc or a sibling dossier disagrees with an asset, the asset is the record and the disagreement is listed in §6.

Abbreviations: `VA` = `Assets/_Scripts/Controller/Animation/VesselAnimation.cs`; `VT` = `Assets/_Scripts/Controller/Vessel/VesselTransformer.cs`; `SST` = `Assets/_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs`; `P/<Vessel>` = `Assets/_Prefabs/Spacevessels/<Vessel>.prefab`; `TRACE` = `python3 trace_controllers.py` (scratchpad, prints parameters, reachable trees, orphans; its BlendTree type labels follow §2.1); `PROBE` = `python3 Tools/Build/vessel_model/fbx_probe.py "<fbx>" --takes` (outputs in `.../scratchpad/takes/<basename>.txt`); `V2` = `python3 -I .../scratchpad/takes/bpy_take_measure2.py <fbx> <mapping> <bones>` (Blender 5.2 bpy: zeroes every pose bone, captures the rest, evaluates each action at its last frame; outputs `.../scratchpad/takes/v2_<vessel>.txt`); `MISSILE` = `.../scratchpad/takes/missile_debug.py`.

Frames. "bpy frame" = the Blender world after `import_scene.fbx` (Z-up). Unity frames per file, with the evidence for each mapping:

| file | FBX header (`PROBE` line 1) | Unity (x,y,z) from bpy (bx,by,bz) | evidence |
|---|---|---|---|
| `SquirrelVessel_CosmicShoresTest1.fbx`, `SparrowModel1.fbx` | `up=+2 front=+1 coord=-0`, UnitScaleFactor 100 | **(bx, bz, by)** | Sparrow in-editor record: `b_ShipGun1.L` head lands on the hand-authored `RightGun` muzzle `(+3.2, 0.4, 1.3)` only under Unity = (−wx, wz, −wy) of the FBX world (`Docs/VESSEL_TAIL_AND_JETS.md:303-310`, Sparrow dossier §1.4); bpy = Rz(180)·world for both files (Sparrow: raw `b_ShipGun1.L` T(+15.14,5.59,1.90)·0.2034 under the armature's R(0,0,180) is world (−3.08,−1.14,0.39) and bpy shows (+3.08,+1.14,+0.39); Squirrel: the armature's file R(71.89,−180,0) prints as bpy euler (−108.11,0,0), which is exactly Rz(180)·Ry(−180)·Rx(71.89) = Rx(251.89)). Identical headers ⇒ identical Unity conversion. **Consequence for the Squirrel: its `bbone_Tail*` chain (bpy +Y) lands at Unity +Z = the flown NOSE, exactly as the Sparrow's `b_Tail*` blades do.** The Squirrel dossier §1.4 assumed the opposite ((fx, fz, fy), "the one claim to confirm in the editor"); it is unconfirmed either way. Editor check that settles it: with `Squirrel.prefab` open, `bbone_Tail9Last` must read local z ≈ **+3.96** (this table) or ≈ −3.96 (the dossier) in the vessel frame. Every pose fact below is written so it holds under both readings (up/down, toward-the-tail-chain / toward-the-pods, `.L`/`.R`). |
| `SerpentExport.fbx` | `up=+2 front=-1 coord=+0`, UnitScaleFactor 100 | (2bx, 2bz, 2by) | Serpent dossier §1.2 (bone heads measured against the prefab's per-bone overrides; the ×2 is the model instance scale). |
| `mantis_shapekey_with_animations.fbx`, `dolphin_shapekey_with_animations.fbx` (Y-up rigs) | `up=+1 front=+2 coord=+0`, UnitScaleFactor 1 | **(−bx, bz, −by)** | Dolphin import fitted in-editor as `0.01·diag(−1,1,1)` (`Docs/VESSEL_CONSTRUCTION.md:745-750`); on the mantis rig this mapping reproduces the Manta dossier §3.3 Unity-space table (pitch up = nose up 24.2°, yaw left = nose to −x, roll left = `Wing.L` tip down 1.19). |

Unity rotation sense used throughout (left-handed; `Quaternion.AngleAxis(θ, axis)`): +θ about +X turns +Z (nose) toward −Y = **nose DOWN**; +θ about +Y turns +Z toward +X = **nose RIGHT**; +θ about +Z turns +Y (up) toward −X = **bank LEFT** (left wing down). A reflection mapping (every row above) flips the sense of a rotation, and `V2` already applies that flip in its `delta rot UNITY AngleAxis(...)` column.

---

## 1. The input → puppetry contract (what the pilot's stick becomes)

- `VA:27-35` (`Update`): `if (InputStatus.Idle) Idle(); else if (VesselStatus.IsSingleStickControls) PerformShipPuppetry(EasedLeftJoystickPosition.y, EasedLeftJoystickPosition.x, 0, 0); else PerformShipPuppetry(YSum, XSum, YDiff, XDiff);` — argument order is **(pitch, yaw, roll, throttle)**.
- `IsSingleStickControls` is set only by `SST:17` and `ScarabVesselTransformer.cs:88`. Transformer per prefab (script guid grep in `P/*`): **Squirrel `VesselTransformer`** (two-stick), **Serpent `SingleStickVesselTransformer`**, **Sparrow `SingleStickVesselTransformer`**, Manta `VesselTransformer`, Urchin `GunVesselTransformer` (two-stick base), Dolphin `VesselTransformer`, Rhino `VesselTransformer`.
- Two-stick axes (`Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs:225-228`, same mix in `DualStickMix.Mix` for keyboard, `DualMouseInputStrategy.cs:229-232`): `XSum = Ease(rx+lx)`, **`YSum = −Ease(ry+ly)`** (stick UP ⇒ YSum < 0), `XDiff = (rx−lx+2)/4 ∈ [0,1]`, **rests at 0.5**, `YDiff = Ease(ry−ly)`. `Ease(v) = ±(cos(v·0.785)−1)` (`DualStickMix.cs:35-40`), so |axes| ≤ 1 and the curve is soft near centre. Single-stick: `EasedLeftJoystickPosition = (Ease(2·lx), Ease(2·ly))` (`GamepadInputStrategy.cs:212-215`, `TouchInputStrategy.cs:532`): stick UP ⇒ +y, saturates at half stick.
- Hull response, two-stick (`VT:921-943`): `Pitch: AngleAxis(+YSum·…, transform.right)` ⇒ **YSum > 0 = nose DOWN**, so stick UP = nose UP; `Yaw: AngleAxis(+XSum·…, up)` ⇒ stick RIGHT = nose RIGHT; `Roll: AngleAxis(+YDiff·…, forward)` ⇒ **YDiff > 0 (right stick up / left stick down) = bank LEFT**.
- Hull response, single-stick (`SST:24-46`): `Pitch: AngleAxis(−joy.y·…, courseTransform.right)` ⇒ stick UP = nose UP; `Yaw: AngleAxis(+joy.x·…, up)` ⇒ stick RIGHT = nose RIGHT; `Roll: AngleAxis(−joy.x·…, forward)` ⇒ stick RIGHT = bank RIGHT (into the turn).
- `InputStatus.Idle` is written by `TouchInputStrategy.cs:218/241`, `InputStatus.cs:276` and `InputController.cs:383`; `RiptideAnimation.cs` (comment above `Idle()`) records that in play only the touch strategy raises it. On gamepad/keyboard the puppetry therefore runs every frame with the raw (decayed) inputs and never takes the `Idle()` branch.
- `Brake(t) = t − brakeThreshold` for `t < brakeThreshold` else 0 (`VA:300-303`; `brakeThreshold` 0.65 on every prefab except Urchin 0.5). Throttle is `XDiff` (0..1, cruise 0.5), so every "throttle" term below carries a permanent cruise offset.

---

## 2. Animator-driven vessels

### 2.1 Facts common to all four controllers

- `m_BlendType` numbering: `0` Simple1D, `1` SimpleDirectional2D, `2` FreeformDirectional2D, `3` FreeformCartesian2D, `4` Direct — the order Unity's ScriptReference lists `UnityEditor.Animations.BlendTreeType` (fetched 2026-10-17; the page gives no numerics, Unity enums number from 0 in declaration order). The Squirrel dossier labels type 2 "SimpleDirectional2D" and the Sparrow dossier labels type 3 "Freeform Directional"; both are the YAML value mislabelled.
- Drivers (`Assets/_Scripts/Controller/Animation/`): `MantaAnimationContoller.cs:16-46` lerps four floats at `animationSpeed 3.25`/s and writes **`Pitch = −2·currentPitch`, `Yaw = 2·currentYaw`, `Roll = 2·currentRoll`, `Throttle = 2·currentThrottle`**; `Idle()` lerps to 0 and writes them ×1 with NO sign flip on Pitch (asymmetric, converges to 0); `Boost` only when `hasBoost`. `SparrowAnimationController.cs:63-93` is identical except `Idle()` writes `−currentPitch`, plus the missile layer (§2.4). `SingleStickAnimationController.cs` (raw ×1 values, writes `Blend = 1`) is referenced by **no prefab** (grep of guid `6def1256cd8b3124a99dc1ff7405448d` in `P/*`: none). Prefab → driver (script guid grep): Squirrel and Serpent → **`MantaAnimationContoller`** (`hasBoost 0`, `P/Squirrel` fid 5500781737155866732, `P/Serpent` fid 6776027772320837218); Sparrow → `SparrowAnimationController` (`hasBoost 1`, `missileExecutor` → fid 4658886838346608747); Termite/Falcon/Shrike also carry `MantaAnimationContoller`.
- **None of the drivers writes `Blend`, `x`, `y`, `pitch/yaw/roll/throttle` (lowercase)**, so those sit at their `m_DefaultFloat`: `Blend` 1 (Squirrel, Serpent, Sparrow) / **0 (Manta controller)**; `x` 1, `y` 1 (Squirrel).
- Parameter ranges the trees actually see: `|Pitch|, |Yaw|, |Roll| ≤ 2`; `Throttle = 2·XDiff ∈ [0, 2]`, **resting at 1.0** on a two-stick hull; on a single-stick hull Roll = Throttle = 0 always. Every 2D tree places its poses at radius 1, so **a pose saturates at half stick** (|stick| = 0.5 two-stick; single-stick `Ease(2·0.5·…)` ⇒ ≈ 0.375 raw).
- Every controller carries orphan sub-assets unreachable from any layer (`TRACE`): Squirrel 12 trees, Serpent 15, Sparrow 14, Manta 10 — including `urchan fly` / `throtle` / `roll` trees binding `urchan_shapekey_with_animations.fbx` clips, trees binding mantis clips, and `BlendTree`s with no motions. They are copy residue; a clip they reference still imports.
- Clip binding: each `.fbx.meta` `clipAnimations[]` entry pins `takeName → internalID`, and the controller's `m_Motion {fileID}` IS that internalID (verified for all 28 bound clips below). A re-export that keeps the take names keeps the binding; a renamed take imports as a new clip and the controller's reference dangles.
- Animator component facts (`P/*` m_Modifications on the FBX-instance Animator, target `5866666021909216657` in each FBX): Squirrel `m_Controller → f076647e…`, `m_CullingMode 0`, **`m_ApplyRootMotion 0`**; Serpent `c758fbec…`, culling 0, **`m_ApplyRootMotion 1`** (its `.meta` names `rootMotionBoneName: HeadRootBone`); Sparrow `3caa5407…`, culling 0, root motion 0.

### 2.2 Squirrel — `Assets/_Animations/SquirrelAnimatorController 1.controller` (guid `f076647eb7655744997dda101929faf0`)

Parameters (`TRACE`; `m_DefaultFloat` from the YAML): `Blend` 1, `Pitch` 0, `Yaw` 0, `Roll` 0, `Throttle` 0, `x` 1, `y` 1 (all float).
Layer `MovementAnimLayer` (override, the only layer), default state **`Blend Tree`, `m_Speed 50`**, motion = the one reachable tree:

```
TOP  type 2 FreeformDirectional2D  params x / y          (x, y never written ⇒ sample sits at (1, 1))
  (1, 0) -> YP   type 2  params Yaw / Pitch:  pitchup (0,+1)  pitchdown (0,-1)  yawright (+1,0)  yawleft (-1,0)  idle (0,0)
  (0, 1) -> RT   type 2  params Roll / Throttle:  speedup (0,+1)  speeddown (0,-1)  rollleft (+1,0)  rollright (-1,0)  idle (0,0)
  (0, 0) -> idle clip
```

Consequences: (a) at (1,1) the TOP tree mixes YP and RT with roughly equal weight and gives the origin `idle` ≈ 0 (the exact freeform-directional weights were not computed; the Squirrel dossier §3.2 states ≈½ each) — **every pose reaches the screen at about half its authored deflection, blended with the other tree's pose**; (b) `Throttle` rests at 1.0 in flight, so at a centred stick the RT tree sits ON `speedup`; `speeddown` at Throttle −1 is unreachable (Throttle ≥ 0) except through the `Idle()` decay toward 0, which lands on `idle`, not `speeddown`; (c) `m_Speed 50` is harmless because every clip is a single-frame pose.

Clip table (`SquirrelVessel_CosmicShoresTest1.fbx.meta`, `name == takeName`, `firstFrame 0 lastFrame 1 loopTime 0`; `TRACE` resolves the motions to these ids):
`a_Squirrel_Armature|squirrelanim_idle` −2403160849616365822 · `…pitchdown` 6027476600366338588 · `…pitchup` 3850382461062512866 · `…rollleft` −8583781936895361006 · `…rollright` −3465231570906512904 · `…speeddown` −5886390736306840818 · `…speedup` 4102268083176662213 · `…yawleft` 7868478854632894093 · `…yawright` −2194766280897318139.

Stick → pose (two-stick, `MantaAnimationContoller`): stick UP ⇒ `YSum<0` ⇒ `Pitch = −2·YSum > 0` ⇒ **`pitchup`**; stick DOWN ⇒ `pitchdown`; stick RIGHT ⇒ `Yaw>0` ⇒ **`yawright`**; `YDiff>0` (bank-left input) ⇒ `Roll>0` ⇒ **`rollleft`**; full throttle ⇒ `Throttle 2` ⇒ `speedup` (already there at cruise).

Takes (`PROBE`: 9 takes, **each 1 key, 0.000 s, curves on all 43 nodes, 0 blend-shape curves**; `idle` moves 0 nodes = it IS the rest pose). Pose-bone values are what a modeller types into Blender (`rotation_euler` XYZ degrees on the pose bone, `location` in bone units); world effects from `V2` with the bones' rest: `bbone_WholeTail` head bpy (0, 2.40, −0.22), `bbone_Tail9Last` (0, 3.96, −0.42) (parent WholeTail), `bbone_FrontEngineTip.L` (1.94, −1.54, 0.23) pointing −Z (down), `bbone_WholeWing.L` (1.06, −0.34, 0.19) pointing +X (span), `bbone_WingPiece6.L` (1.32, 1.07, −0.33) (parent WholeWing.L, on the tail-chain side), helmet/ears at bpy −Y (the pods' side). "T-side" = the tail chain's end (bpy +Y), "P-side" = the pods/face end (bpy −Y).

| take | pose-bone keys (bpy local euler deg / loc) | measured world effect (`V2`) |
|---|---|---|
| `pitchup` | tail chain local Y ramps **−6.5 (Tail1First), −8.9, −11.4, −13.8, −16.2, −18.7, −21.1, −23.6, −26.0 (Tail9Last)**; `WholeWing.L (1.4, −15.9, −0.3)`, `.R (−1.4, −15.9, 0.3)`; `WingPiece1..6.L/R` ≈ 16 (`PROBE` 15.8-16.0); `FrontEngineTip.L (−1.0, −0.1, −14.0)`, `.R (1.0, −0.1, 14.0)`, `FrontEngineBase.L (−2.4, −0.4, −13.8)`, `FrontEngineTrim` 13.6; `UpperHelmet (0, 10, 0)`, `LowerHelmet` 10, `Ears (0, 14, 0)` | each tail segment turns **26° (Tail9Last) about the lateral axis so the chain's free end goes DOWN**: `WingPiece6.L` tail −0.43 up, +0.25 toward P-side; the front pods' lower ends swing 0.24 toward the P-side (the pods tilt with their tops toward the T-side); helmet/ears tilt the opposite way to the tail (10°/14°) |
| `pitchdown` | exact sign mirror of pitchup (tail +6.5…+26, wings (−1.4, 15.9, −0.1), pods (1.0, −0.1, 14.0), helmet (0, −10, 0), ears (0, −14, 0)) | tail free end UP (`WingPiece6.L` +0.49), pod bottoms swing 0.24 toward the T-side |
| `rollleft` | `WholeWing.L (2.8, −29.9, −0.9)`, **`WholeWing.R (2.7, +29.9, 0.6)`** (opposite twist about the span), `WingPiece1..6` 29.5-30.0, tail chain twist ramps 3.6 (Tail2) → `Tail9Last (−26.1, −2.2, −9.4)`, `cheek.L/.R` 9.5 | **the `.L` fan drops** (`WingPiece6.L` tail −0.73, head −0.64) and the `.R` fan rises (mirror); the tail tip rises 0.41 and shifts 0.10 sideways (28° about the hull's long axis) |
| `rollright` | mirror of rollleft | `.R` fan drops, `.L` rises (`WingPiece6.L` +0.92 up) |
| `yawleft` | `WholeTail (0.8, −0.1, 8.0)`, `Tail1First (2.7, −0.1, 5.4)`, chain cumulative 5.8/8.8/11.7/14.8/18/21.2/24.3/28.2 → `Tail9Last (−10.0, 2.5, 28.0)`; **`WholeWing.L (3.7, −0.2, 13.5)` and `.R (3.6, −0.7, 13.5)` with the SAME sign** (both swing the same way about the up axis), pieces 13-14; `UpperHelmet (0.2, 0, 9.0)`, `Ears (8.6, 0.4, −5.1)` | the tail bends about the UP axis (WholeTail 8°, Tail1First 14°, Tail9Last 38° cumulative) and its tip moves 0.22 **toward the `.L` side**; both wing fans rotate 14° (28° at `WingPiece6`) about the up axis the same way as the tail; the helmet turns 9° the opposite way (the body bends into a C) |
| `yawright` | mirror | tail tip toward the `.R` side |
| `speedup` | `FrontEngineTip.L/R loc (0.16, 0.002, −0.011)`, `Base (0.098, …)`, `Trim 0.133` (translations only); `WholeWing.L (−4.6, −1.0, −17.3)`, `.R (4.6, −1.0, 17.3)`, pieces 16.7-18.1; `Tail9Last (0, 4, 0)`; `Ears (0, −10, 0)` | pods translate **0.16 toward the P-side**; wing fans fold 18° (36° at Piece6) about the up axis: `WingPiece6.L` tail moves 0.70 inboard and 0.52 toward the T-side (the fans sweep toward the tail chain) |
| `speeddown` | pods `loc (−0.12, −0.002, 0.008)` (Base 0.079, Trim 0.095); `WholeWing.L (4.2, −6.2, 13.1)`, `.R (−4.2, −6.2, −13.1)`, pieces 12.4-13.8; `Tail9Last (0, −6, 0)`; `Ears (0, 6, 0)` | pods 0.12 toward the T-side; fans spread 15° (31° at Piece6) outboard 0.27, 0.62 toward the P-side and 0.15 down (airbrake) |

Reading in Unity. Under the header-rule mapping (bx, bz, by) the T-side is the nose: `pitchup` then drops the NOSE (the leading tail chain) while the hull is pitching up — a counter-lean, like the Sparrow (§2.4) — and the `.L` bones are on the pilot's RIGHT. Under the dossier's assumption the T-side is aft: `pitchup` drops the tail and lifts the pods = a lean WITH the stick and honest names. Up/down, inboard/outboard and `.L`/`.R` facts above are reading-independent. (The `PROBE` "offset" column is a different metric — the norm of the FBX `Lcl Rotation` Euler difference, e.g. `WholeWing.L` 80.4 on pitchup — not the pose-bone delta; quote the `V2` values.)

### 2.3 Serpent — `Assets/_Animations/SerpentAnimController.controller` (guid `c758fbec7faf0804f99bee5cb830a388`)

Parameters: `Blend` 1; `pitch, yaw, roll, throttle` (lowercase, written by nothing); `Pitch, Yaw, Roll, Throttle` 0. One layer `MovementAnimLayer`, default state `SerpentBaseAnimationsBlendTree` (speed 1), motion:

```
TOP  type 4 Direct  (one child, direct param Blend = 1 => full weight)
  -> YP  type 3 FreeformCartesian2D  params Yaw / Pitch:
       SerpentUpAnim (0,-1)   SerpentDownAnim (0,+1)   SerpentRightAnim (+1,0)   SerpentLeftAnim (-1,0)
```

No idle clip in the tree (at (0,0) the four poses blend to ≈ rest by symmetry); `Roll`/`Throttle` drive nothing; `SerpentIdleAnim` and `SerpentSpeedIncreaseAnim` are imported but unbound. Clip ids (`SerpentExport.fbx.meta`, frames 0..1): Down 8544842894480929770, Idle 6649277073756093042, Left 4294602363674685341, Right 3182847114785585752, Up −8897570794599522389, SpeedIncrease 102859810310046030. The prefab also instances `serpent.fbx` (one take `Take 001`, 2.458 s, 1 blend-shape curve, nothing bound — `PROBE serpent.txt`).

Stick → pose (single-stick): stick UP ⇒ `Pitch = −2·joy.y < 0` ⇒ (0,−1) ⇒ **`SerpentUpAnim`**; stick RIGHT ⇒ `Yaw > 0` ⇒ **`SerpentRightAnim`**.

Takes (`PROBE`: 6 takes, 1 key, 0.000 s, 28 nodes keyed; the probe reports 0 blend-shape curves while bpy lists four `Key|…` shape actions pinned at 0 — see §6). Rest: `HeadRootBone` head bpy (0, −2.07, 0) = vessel (0, 0, −4.14), the ROOT of the skin; `ChassisBone` and `EngineBone` are its children; tendrils `HeadTail1..5.L/R`, `BotTail1..5.L/R` hang off it (`V2`, Serpent dossier §1.2).

| take | pose-bone key | measured effect (`V2`, Unity = (bx,bz,by), scale ×2 on the prefab) |
|---|---|---|
| `SerpentIdleAnim` | identity on every bone | rest |
| `SerpentUpAnim` | `HeadRootBone rotation (0, +30, 0)` | delta `AngleAxis(−30°, +X)` = **whole hull nose UP 30°** about the root at z −4.14; `HeadTail5.L` head moves (0, −3.79, −2.20) file units (tendril tips drop as the nose rises) |
| `SerpentDownAnim` | `(0, −30, 0)` | `AngleAxis(+30°, +X)` = nose DOWN 30°; tendril tips rise (`HeadTail5.R` +2.18 up) |
| `SerpentLeftAnim` | `(−25, 0, −25)` | one 35.2° turn about (−0.15, −0.70, +0.70): **nose LEFT 25° plus a 25° bank, left side down**; `BotTail5.R` head moves (+3.51, +1.74, +2.52) (the right-side tendril swings outboard and up) |
| `SerpentRightAnim` | `(+25, 0, +25)` | mirror: nose right, right side down |
| `SerpentSpeedIncreaseAnim` | `ChassisBone location (−3.566, 0, 0)`, `EngineBone (1.09, 0, 0)` | carapace plate slides **3.57 file units (7.13 vessel units) aft**, engine 1.09 (2.18) aft: a telescoping stern. **Not bound.** |

Direction convention, closed: the poses lean the whole skin INTO the turn the hull is making (same sense, 25-30° on top of the real rotation); `m_ApplyRootMotion 1` + `rootMotionBoneName: HeadRootBone` means Unity may apply the root bone's rotation to the model transform instead of the bone — same lean on screen, but keep `HeadRootBone` the skeleton root.

### 2.4 Sparrow — `Assets/_Animations/SparrowAnimatorController.controller` (guid `3caa5407b15392f4a9593ce16c77314b`; also on `Scarab.prefab`)

Parameters: `Blend` 1; lowercase four (unused); `Pitch, Yaw, Roll, Throttle`; **`Boost` (bool)**. Two layers.

Layer 0 `MovementAnimLayer` (override), default state **`SparrowFlyingBlendTree`** (`writeDefaults 1`):

```
TOP type 4 Direct, both children direct-weighted by Blend (=1)
  -> YP  type 3 FreeformCartesian2D  Yaw / Pitch:  PitchUp (0,-1)  PitchDown (0,+1)  YawRight (+1,0)  YawLeft (-1,0)
  -> RT  type 3  Roll / Throttle: four children with m_Motion {fileID: 0}  (EMPTY - contributes nothing)
state SparrowSpeedBlendTree: same shape with Speed+PitchUp (0,-1)  Speed+PitchDown (0,+1)  Speed+TurnRight (+1,0)  Speed+TurnLeft (-1,0)  + the same empty RT
transitions (YAML &-1290543145658869037 / &-5033323461545527613):
  BoostTransition   Flying -> Speed : condition Boost == true  (mode 1), hasExitTime 1 with exitTime 0, fixed duration 0.998 s, offset 0
  UnboostTransition Speed -> Flying : condition Boost == false (mode 2), same, duration 0.997 s
```

Layer 1 **`Missile Launching`**: `m_BlendingMode 1` (Additive), `m_DefaultWeight 0`; states `Missile Launch 1` (default) and `Missile Launch 2`, **`m_Speed 2.5`**, no transitions; motions in `SparrowModel4.fbx` (guid `ebe998f747168104ca3e85b7295e47be`): `Sparrow_Armature+Mesh|Missile Launch 1` 2132225776441424335, `…Launch 2` 3552259950087936227 (frames 0..22, loopTime 0). Driver (`SparrowAnimationController.cs:97-121`): on `FireGunActionExecutor.OnMissileFired(usedRightBay)` → `SetLayerWeight(1, 1)`, `Play("Missile Launch 1" if right bay else "Missile Launch 2", 1, 0f)`, poll `normalizedTime ≥ 1`, then weight 0. Orphans: a Throttle tree with `SpeedUp` (0,1) / `SpeedDown` (0,−1) is unreachable; `Idle_SparrowAnim` is referenced by nothing.

Clip ids (`SparrowModel1.fbx.meta`, frames 0..1): Idle 1538366950322441532, SpeedDown −7745171902883792618, SpeedUp −8790746044482621832, YawLeft 4688057880925959930, YawRight 4977852477300310412, PitchUp 4873371448912438895, PitchDown −39112737550917836, Speed+PitchDown 2854880104358996654, Speed+PitchUp −788958886855914976, Speed+TurnLeft −7939550155984146610, Speed+TurnRight −3453335285089842673. `SparrowModel4.fbx.meta` carries `globalScale 100` (its header is Y-up with UnitScaleFactor 1, unlike Model1's 100) and lists the flight poses under a doubled prefix `Sparrow_Armature+Mesh|Sparrow_Armature+Mesh|…` (a Blender re-import/re-export residue) — only its two launch clips are bound.

Stick → pose (single-stick, `hasBoost 1`): stick UP ⇒ `Pitch<0` ⇒ **`PitchUp_SparrowAnim`** (or `Speed+PitchUp` while boosting); stick RIGHT ⇒ `YawRight`; Roll/Throttle 0 always. With the ×2 input the poses saturate at ≈0.375 raw stick.

Takes (`PROBE`: 12 takes in Model1, every flight take 1 key at frame 1, 66 nodes keyed, 0 blend-shape curves in this run; `V2` with Unity = (bx, bz, by): the fan root `b_SpinWing.L` rests at Unity (+1.73, 0.74, +0.65), its piece chain `b_SpinWing.L.001..008 → b_WingPiece*` spans z −1.01 (`WingPiece1First`, the aft piece) to +2.28 (`WingPiece8Last`, nose side); the six `b_Tail1..3.L/R` blade bones at z +4.54..+5.30 (the flown NOSE); `.L` bones at Unity +x = pilot's RIGHT):

| take | pose-bone keys | measured effect |
|---|---|---|
| `Idle_SparrowAnim`, `.006` | all zero | rest |
| `PitchDown_SparrowAnim` | `b_SpinWing.L/.R (0, +16, 0)`; `b_SpinWing.L.008 (1.3, 15.9, 0.3)`; `b_Tail1.L (−2.4, 0.5, 23.9)`, `Tail2.L (1.6, −0.3, 23.9)`, `Tail3.L (9.5, −1.8, 21.8)` (24 bones) | fan roots turn 16° (pieces compound to **32°**) about the lateral axis: the aft piece `WingPiece1First.L` drops 0.73 and moves 0.85 forward, the nose-side `WingPiece8Last.L` **rises 1.41** — the drawn +z end rises = a **nose-UP lean on the clip named PitchDown**; blades 24° |
| `PitchUp_SparrowAnim` | sign mirror (`SpinWing (0, −16, 0)`, blades −24) | aft piece rises 1.12, nose piece drops 1.03 = **nose-DOWN lean on the clip the stick-UP command plays** (counter-lean; the model flies tail-first) |
| `YawLeft_SparrowAnim` | **`b_SpinWing.L` and `.R` both `(−13.8, −2.4, −9.7)`** (same sign); `SpinWing.L.008 (16.6, −1.8, −4.5)`; `b_Tail1..3.L (0, 22, 0)` | fan roots 17° (pieces 34°) about an axis (−0.07, −0.81, −0.58) ≈ the up axis, nose-left sense; blades 22° about the up axis; the piece chain folds asymmetrically (dossier §3.4: inside fan 25→80° per piece) |
| `YawRight_SparrowAnim` | mirror | |
| `SpeedUp_SparrowAnim` | `b_SpinWing.L (22, 0, 0)`, `.R (−22, 0, 0)`; `SpinWing.L.008 (−13.9, 3.1, 16.5)` (pieces fold progressively — `PROBE`: .001 33.3°, .002 32.8°, … .008 30.3° each, stacking); `b_Tail1..3.L loc (0.348, 0, 0.037) / (0.349, 0, −0.024) / (0.32, 0, −0.142)`; `b_ShipGun1/2.L/R loc (0.25, 0, 0)` | fans fold **22° at the root, 44° by `.008`**, the nose-side piece `WingPiece8Last.L` moves 1.88 aft and 1.10 inboard, the aft piece 2.12 forward (the fan stacks at the root); blades slide **0.07 u forward**, guns 0.05 u aft |
| `SpeedDown_SparrowAnim` | `b_SpinWing.L (−20.1, −5.6, 2.1)`, `.R (20.1, −5.6, −2.1)`; `.008 (12.5, −5.0, −16.6)`; blades `loc (−0.249, 0, −0.026)`…; guns 0.25 | fans spread **21° (42° by .008)**: `WingPiece1First.L` 1.78 outboard / 0.46 up / 1.26 aft; blades slide 0.05 u aft |
| `Speed+PitchUp/Down` | **identical to PitchUp/Down** + the four `b_ShipGun*` `loc (0.25, 0, 0)` (`PROBE` 28 nodes = 24 + 4; `V2` fan values byte-equal) | the fans are NOT folded in these clips (the Sparrow dossier's "fans folded 58→169°" for Speed+Pitch is not in the file) |
| `Speed+TurnLeft/Right` | identical to YawLeft/Right + the four guns 0.25 | |
| `Missile Launch 1` (Model4) | **MOTION**, frames 1-23 @ 25 fps = 0.880 s, 21 keys (19 on the missile); `b_Missile.R location x` 0 → **30.93** peak → 0, `z` 0 → 3.98 → 0; `b_Shell1.R loc` peak (3.54, −0.50, −1.39) + `rot_quat w 0.991 / z −0.131` (≈15°); `b_Shell2.R loc` peak (0, −0.57, 3.65) + `rot_quat x 0.188` (≈21.7°); **every curve returns to 0 at the last key** (`MISSILE`) | the missile travels 30.93 bone units = **6.29 u** (×0.2034 armature scale) and is back at rest by the end; at state speed 2.5 the clip plays in 0.35 s; `b_Missile.R` is at Unity x −0.58 = the pilot's LEFT bay (the "right bay" in `SparrowAnimationController.cs:10-11` is naming only) |
| `Missile Launch 2` | mirror on `b_Missile.L`, `Shell1.L` (3.54, +0.50, −1.39) ≈14.8°, `Shell2.L` (0, +0.72, 3.65) ≈20.3° | |

### 2.5 The Manta/Urchin controller — `Assets/_Models/Animations/MantaAnimatorController.controller` (guid `f922b4ddeaa211f498ff3a539e6ce230`) — present on five prefabs, driving nothing

Parameters: **`Blend` default 0**, `Pitch, Yaw, Roll, Throttle` 0. `Base Layer`, state `Blend Tree`:

```
TOP type 4 Direct, both children direct-weighted by Blend (= 0 and never written => both weights 0)
  -> YP  type 3  Yaw / Pitch:  pitch down (0,+1)   pitch up (0,-1)   yaw right (+1,0)   yaw left (-1,0)
  -> RT  type 3  Roll / Throttle:  speedup (0,+1)   speeddown (0, 0)   roll left (+1,0)   roll right (-1,0)
```

Clips from `mantis_shapekey_with_animations.fbx` (guid `7c324ef5…`): pitch down 6212661447819486376, pitch up 3804817242277331963, yaw right −6822812431335815528, yaw left −6953860090002917003, speedup −7942830232961210933, speeddown 1932594824745245842, roll left −8679543024212152096, roll right −721875957892904936 (plus `cut` 2905659382556172114, unbound). The `.meta` slices **`firstFrame 19, lastFrame 20`** of 21-key motions ⇒ Unity sees single-frame POSES at the take's full deflection. Note `speeddown` sits at (0,0), not (0,−1).

Where it is wired: `P/Manta` — the Animator with this controller (fid 1619232438159974630) is on GO `mantis_shapekey_with_animations`, **`active=0`** (`manta_prefab_tree.txt:408`); the live `Manta_shapekey_rigged` GO's Animator (fid 5658933110804061391) has **`m_Controller {fileID: 0}`**. `P/Urchin` — Animator fid 6795854866240994547 on an empty GO `Animation` (child of `OrientationHandle`, no children, `m_Avatar 0`): the clips' paths bind nothing. Termite/Falcon/Shrike also reference it. Nothing writes its parameters (no driver on the Manta/Urchin prefabs has an `animator` field), and `Blend = 0` zeroes both children anyway — **this controller cannot move anything on any shipped prefab**.

Takes measured anyway (`V2` on the mantis rig, mapping (−bx, bz, −by); all **MOTIONS, 21 keys, frames 1-21 @ 24 fps = 0.833 s**; `fusal` = the fuselage bone, head at the origin, bind direction +z; `Wing.L` head Unity (−1.0, 0, 0.26) tail (−1.55, 0, 1.10)):

| take | `fusal` key (last frame) | wings / fins | reads as |
|---|---|---|---|
| `pitch up` | `rotation (−24.2, 0, 0)` ⇒ `AngleAxis(−24.2°, +X)` | `Wing.L/.R (2.2, ±0.3, ∓11.9)`, tips 0.60 down; `WingB.L (−3.7, −1.5, −30.5)` | **nose UP 24.2°** |
| `pitch down` | `(23.3, 0, 0)` | `Wing.L (10.9, 5.5, −16.2)`, tips 0.47 up; `WingT.L (3.6, 10.8, −30.4)` | nose down 23.3° |
| `yaw left` / `yaw right` | `(0, 0, −20.9)` / `(0, 0, 22.1)` ⇒ `AngleAxis(−20.9°, +Y)` / `(+22.1°)` | wings swing 17-30° about the up axis, `WingT/B.L` 25-47° | nose LEFT / RIGHT |
| `roll left` / `roll right` | `(0, −48.7, 0)` / `(0, 46.3, 0)` ⇒ `AngleAxis(+48.7°, +Z)` / `(−46.3°)` | `Wing.L (−16.2, −3.7, −1.0)`: **`.L` tip down 1.19**, `.R` tip up 1.19 | bank LEFT / RIGHT |
| `speedup` | 0 | `Wing.L (0, 0, 5.5)`, `Winghold.L (0, 0, 18.4)`: tips 0.48 inboard, 0.46 aft; fins `WingT/B` 41-43° | wings fold aft |
| `speeddown` | 0 | `Wing.L (0, 0, −8.3)`, `Winghold.L (0, 0, −26.5)`: tips 0.34 outboard, 0.92 forward | airbrake |
| `cut` | 0 | `WingThold.L (4.1, −6.8, 0.4)`, `WingT.L (−3.4, −0.1, −0.5)` only | a stub |

Each mantis take also carries four `DeformPercent` shape curves constant 0 (Manta dossier §3.3).

---

## 3. Code-puppetry vessels (no Animator in the loop)

Shared base facts (`VA`): `ResolvePart(authored, …candidates)` (`VA:86-105`) returns the authored reference if wired, else the first descendant (case-insensitive, `GetComponentsInChildren<Transform>(true)`, first occurrence wins) matching the candidates in order; misses are reported once by `ReportUnresolvedParts` (`VA:112-120`) and the limb simply does not move. `CaptureRestRotations` (`VA:148-157`) stores `localRotation` and `inverse(vessel.rotation) * part.rotation`; `CaptureRestPositions` (`VA:263-268`) stores the rest position in the vessel frame. `RotatePartFromRest`: `target = Euler(pitch, yaw, roll) * rest`, lerp at `lerpAmount·dt` (2/s) (`VA:180-189`) — Euler on the LEFT ⇒ the three angles are about the part's **PARENT's** axes. `RotatePartFromRestInFrame(part, turn, frame)`: `targetWorld = frame * turn * restInVessel`, localized through the live parent (`VA:230-245`) ⇒ the turn is about the FRAME's axes (the vessel's) whatever the bone axes are. `MovePartInFrame` localizes a vessel-frame point through `parent.InverseTransformPoint` ⇒ exact through any bone-chain scale (`VA:306-318`). Legacy overloads: `RotatePart(part, p, y, r)` = absolute `Euler(p, y, r)` (identity rest assumed) and **`RotatePart(part, pitch, yaw, roll, initial)` = `Euler(pitch, ROLL, YAW) * initial`** — the 2nd/3rd arguments are swapped onto Euler Z/Y (`VA:384-393`). `Idle()` lerps only the parts in `Transforms` back to `InitialRotations[i]` (if `SaveNewPositions`) or to `RestRotationOf(part)` at `smallLerpAmount·dt` (0.7/s) (`VA:322-348`).

### 3.1 Dolphin — `RiptideAnimation.cs` (`P/Dolphin` fid 7912223775244125494; every part field authored `{fileID: 0}` ⇒ all resolve by name)

Candidates (`RiptideAnimation.cs`, `ResolveParts`), first = the rig's bone, then legacy names: `Chassis`: `fuse`, `Chassis`, `Dolphin_Test` · `LeftWing`: `wing.l`, `LeftWing` · `RightWing`: `wing.r`, `RightWing.001`, `RightWing` · `ThrusterTopLeft/TopRight`: `jetT.l`/`jetT.r`, `Engine case Left.1`/`Right.1` · `ThrusterLeft/Right`: `jetm.l`/`jetm.r`, `Engine case Left.2`/`Right.2` · `ThrusterBottomLeft/BottomRight`: `jetB.l`/`jetB.r`, `Engine case Left.3`/`Right.3` · `topJaw` AND `NoseTop`: `jaw.u`, `TopNose` · `bottomJaw` AND `NoseBottom`: `jaw.b`, `bottomNose`. Trap: the legacy names still exist in the prefab as collider-host GameObjects, so a renamed bone silently binds a collider host.
Rest capture: `CaptureRestRotations` on all 13 parts, `CaptureRestPositions` on chassis, wings and the six cases; `_chassisPivot` = the chassis's rest position (0 on this rig). **The rest ORIENTATION of a bone is free; its HEAD POSITION is load-bearing** (jets, colliders and the pivots below were measured against the heads).
Maths (`PerformShipPuppetry`), `animationScaler 25`, `exaggeratedAnimationScaler 75`, inputs raw stick axes (−1..1), throttle `XDiff` 0..1:
- chassis: `chassisTurn = Euler(pitch·25, yaw·25, roll·25)` in the VESSEL frame (`RotatePartFromRestInFrame(Chassis, chassisTurn, transform.rotation)`), no sign flips (`mirrorAppendageRoll` ships OFF).
- wings: own turn `Euler(Brake(t)·25, yaw·75 ± sweep, (roll ± pitch)·25)` with `sweep = max(0, t − 0.5)·66` (right `+sweep, +pitch`; left `−sweep, −pitch`); placed by the legacy CHASSIS CHAIN: orientation `chassisTurn * ownTurn * rest`, position `chassisPivot + chassisTurn * (flightPivot + ownTurn * (rest − chassisPivot − authoredPivot))` with `wingAuthoredPivot (0,0,0.1)`, `wingFlightPivot (0,0,0) + wingStationForward 0.5811` on z (`ChainPosition`).
- six engine cases: one shared `Euler(pitch·75, yaw·75, roll·75)` about `thrusterFlightPivot (0, 0.15, −1.7)`, geometry from `thrusterAuthoredPivot (0, 0.14726, −2.04703)` ⇒ at zero input the cases sit +0.347 z forward of their rig rest; each bone's own rest fan-out is carried through untouched.
- jaws: `localRotation = Euler(∓angle, 0, 0) * rest`, `angle = atan(lerp(tan 4.7636°, tan 23.4287°, energy))` from `ResourceSystem.Resources[0]` (`calculateBlastAngle`); not stick-driven; the upper jaw negative X, lower positive.
- drift (`VesselStatus.IsDrifting`): fuselage + jaws keep aiming; wings and cases are written EXACTLY in a Course-aligned cage `FromToRotation(forward, Course) * rotation` (antipode hold at dot < −0.999, slew limit 360°/s), blending from the adopted entry pose to rest orientation + offsets (wings `+3.5` z, cases `−1.25` z, world units) at `lerpAmount 2`.
- idle: a stick-idle drift calls `PerformShipPuppetry(0,0,0,0)`; otherwise rotations relax to rest and `ApplyRestingLayout` reseats positions.
Parent chain the code assumes: the rig's `fuse` at the vessel origin; each wing under its own `winghold.*`, each jet under `jethold{T,m,B}.*`; the chain is reproduced in vessel space, so bone-local axes are irrelevant — the only orientation requirement is that the **rest pose is the ship's own frame (nose +Z, up +Y, starboard +X)**.
Rig takes (`dolphin_shapekey_with_animations.fbx`, Dolphin dossier §3.4 from `PROBE`): ten MOTIONS, 21 keys / 0.833 s, `dolphin|{jawcharge,pitchup,pitchdown,yawleft,yawright,rollleft,rollright,speedup,speeddown,startcharge}_dolphin`, none bound (`clipAnimations []`, no Animator), each carrying four shape curves CONSTANT 100.

### 3.2 Manta — `MantaAnimation.cs` (`P/Manta` fid 6293426128044408357, parts AUTHORED, no `ResolveParts`)

Parts (authored → prefab node = FBX bone of `Manta_shapekey_rigged.fbx`): `Fusilage → chassis` (the root bone under `Armature.002`, local rot (0,0,1,≈0) = 180° about Z, parent `Armature.002` R(−90,0,0) S(100)), `LeftWing → Wing.L` (parent `Winghold.L`, rest local z −48.44°, hold z +100°), `RightWing → Wing.R` (parent `Winghold.R`, hold z −100°), `ThrusterTopRight → WingTophold.Right` (parent `chassis.003`, rest euler (−12.164, −40, −65.111)), `ThrusterTopLeft → WingTophold.Left` ((−12.164, 40, 65.111)), `ThrusterBottomLeft → WingBottomhold.Left` ((8.067, −35, 65.111)), `ThrusterBottomRight → WingBottomhold.Right` ((8.067, 35, −65.111)) (`manta_prefab_tree.txt:320-390`). The prefab's hold bones are re-posed away from the FBX bind pose (Manta dossier §3.2).
Scalers on the prefab: `smallAnimationScaler 27`, `mediumAnimationScaler 30`, `bigAnimationScaler 60` (C# defaults 10/30/45 overridden). Rest: `AssignTransforms` records `InitialRotations[0..6]` = the prefab's native `localRotation` in the order chassis, Wing.L, Wing.R, WingTophold.Left, WingTophold.Right, WingBottomhold.Left, WingBottomhold.Right, and the puppetry pre-multiplies onto them (`RotatePart(part, p, y, r, initial)` ⇒ **`Euler(p, r, y) * initial`**: the call's "yaw" lands on the parent's Z, "roll" on Y). Maths (`MantaAnimation.cs:20-64`):
- chassis: `Euler(pitch·30, −roll·30, yaw·30)` in `Armature.002`'s frame (X = +x, Y = −z, Z = +y in vessel space, Manta dossier §3.1) ⇒ pitch about +x, roll about the long axis, yaw about +y, 30°/unit each.
- Wing.L: `Euler(pitch·30, (−roll−pitch)·27, (−throttle+yaw)·60)`; Wing.R: `Euler(pitch·30, (−roll+pitch)·27, (throttle+yaw)·60)` in the holds' frames ⇒ pitch FLAPS the wing (±30°), roll±pitch TWISTS it about its span (±27°), throttle and yaw SWEEP it in the plane (60°/unit: 30° swept at cruise `XDiff 0.5`, 60° at full).
- the four fin holds: `Euler(pitch·30, (−roll ± throttle)·27, −yaw·30)` (`+throttle` on BottomLeft and TopRight, `−` on TopLeft and BottomRight) ⇒ fins pitch with the hull, counter-yaw, and throttle scissors the two diagonal pairs (13.5° at cruise).
Signs: no compensation anywhere; `+pitch` (= `YSum > 0` = nose down) drops the chassis nose = the parts deflect in the SAME sense as the hull. A replacement's rest must be the ship's frame and its bone parents must keep these frame orientations or each term lands on a different physical axis. Idle: `SaveNewPositions 1`, all seven parts relax to `InitialRotations`.

### 3.3 Rhino — `RhinoAnimation.cs` (`P/Rhino` fid 3496801340156507927, parts AUTHORED to the part-per-mesh placeholder)

Candidates: `Fusilage`: `fuse`, `Fusilage`, `Rhino_Test (1)` · `LeftWing`: `wing1.l`, `Wing front left` · `RightWing`: `wing1.r`, `Wing front right` · `LeftEngine`: `jet.l`, `engine left` · `RightEngine`: `jet.r`, `engine right`. `CaptureRestRotations` on all five (identity on the shipped art; the committed rig rests `wing1.l` ≈ 42°, `jet.l` ≈ 115°). Scalers `animationScaler 25`, `yawAnimationScaler 80`. Maths (`RotatePartFromRest`, Euler about the PARENT's axes):
- Fusilage (the whole model rides it): `Euler(pitch·25, yaw·25, roll·25)`.
- LeftWing: `Euler(0, −Brake(t)·80, (−1+t)·80)`; RightWing: `Euler(0, +Brake(t)·80, (1−t)·80)` ⇒ at throttle 0 the wings yaw ±52° and roll ∓80°; at throttle 1 they rest.
- LeftEngine (child of LeftWing): `Euler(0, +Brake(t)·80, −(−1+t)·80)`; RightEngine: `Euler(0, −Brake(t)·80, −(1−t)·80)` ⇒ the pods cancel the wings' terms and hold their world orientation.
`Transforms` for Idle = LeftWing, RightWing, Fusilage (the engines are not relaxed). Single-stick hulls pass throttle 0 ⇒ permanent full-brake wing pose. Same-sense signs as the hull. Rig takes (`rhino_shapekey_with_animations.fbx`, Rhino dossier §3.4): `rhino` (rest POSE, 2 keys) + eight MOTIONS `rhino_{pitchdown,pitchup,rollleft,rollright,yawleft,yawright,speedup,speeddown}` (21 keys / 0.833 s), `pitchup` = nose up 2.87-3.36 probe units, `yawleft` = nose left 1.61, `rollleft` = crest to the left, `speedup` sweeps the plates in/aft, all with four constant shape curves; none bound.

### 3.4 Urchin — `UrchinAnimation.cs` (`P/Urchin` fid 4005043449782909879, parts AUTHORED, `animationScaler 30` on the prefab, `brakeThreshold 0.5`)

Candidates: `Body`: `fuse`, `Body` · `LeftGun/RightGun`: `gunM.l`/`gunM.r`, `LeftGun`/`RightGun` · `JetTopLeft/Right`: `jetT.l`/`jetT.r`, `JetTopLeft`/`JetTopRight` · `JetBottomLeft/Right`: `jetB.l`/`jetB.r`, `JetBottomLeft`/`JetBottomRight` · `ShroudTopLeft/Right`: `wingconrotT.l`/`.r`, `ShroudTopLeft`/`Right` · `ShroudBottomLeft/Right`: `wingconrotB.l`/`.r`, … · `ShroudLeft/Right`: `sheildconrot.l`/`.r`, `ShroudLeft`/`Right`. `CaptureRestRotations` on Body, the two guns and the four jets (identity on the shipped part-per-mesh art, all nodes under `Body`; the committed rig rests `gunM.l` (90.14, −0.22, 0.96), `jetT.l` (76.31, 18.41, −2.0), `jetB.l` (74.88, −17.17, 2.72)).
Maths: every driven part gets **`Euler(pitch·30, yaw·30, roll·30) * rest`** about its parent's axes (= the vessel's on the shipped art); Body gets the same unless `VesselStatus.IsAttached`, when it is driven toward `Euler(dt·100, 0, 0)` (≈1.7° at 60 fps = holds still while riding). The six shrouds are resolved but **never driven** (they ride their jet/gun parents). Scale puppetry: the four jets lerp `localScale` to 1.75 when free, 1.0 when attached, at 6/s. `AssignTransforms` adds nothing ⇒ `Idle()` resets no part (parts stay where the last puppetry frame left them). Same-sense signs. Rig takes (`urchan_shapekey_with_animations.fbx`, Urchin dossier §3.4): eleven MOTIONS `urchan_{ball,fly,pitchdown,pitchup,roll,rollleft,rollright,speeddown,speedup,yawleft,yawright}` (21 keys / 0.833 s; `roll` 18 keys / 1.667 s = a full 360° barrel roll), four shape curves each, none bound.

---

## 4. TAKE CONVENTION for a new model

1. **Name**: the FBX AnimationStack is `<ArmatureObjectName>|<ActionName>` as Blender's exporter writes it; Unity's clip name and the `.meta` `takeName` are that string verbatim (`SquirrelVessel_CosmicShoresTest1.fbx.meta`: `a_Squirrel_Armature|squirrelanim_idle`; `SerpentExport.fbx.meta`: `SerpentArmature|SerpentUpAnim`; `SparrowModel1.fbx.meta`: `Sparrow_Armature+Mesh|PitchUp_SparrowAnim`). The armature OBJECT name is therefore part of the contract. **Never round-trip a file through Blender and re-export**: the importer prefixes the armature name again and the re-export carries `A|A|take` (bpy shows every shipped take as `a_Squirrel_Armature|a_Squirrel_Armature|…`; `SparrowModel4.fbx.meta` lists its flight poses under the doubled prefix, bound by nothing). For an in-place replacement keep the nine/six/eleven names exactly: the `.meta` pins `takeName → internalID` and the controllers reference the ids (§2).
2. **Kind**: the Animator-driven vessels bind single-frame POSES — one key per bone at frame 1, `frame_range (1,1)`, 0.000 s (`PROBE`/`V2` on all 9 Squirrel, 6 Serpent, 11 Sparrow takes); Unity's `.meta` expands each to `firstFrame 0, lastFrame 1`. The mantis route (21-key MOTION sliced to its last frame by the `.meta`) also yields a pose but needs the slice authored in the editor — do not rely on it. MOTIONS are for one-shots on an additive layer (Sparrow `Missile Launch 1/2`: 21 keys over 0.88 s, every curve returning to 0 so the additive layer leaves no residue) or documentary intent (the Dolphin/Rhino/Urchin rigs' 0.833 s takes, which no controller plays).
3. **Key every bone** in every pose, including `idle` with all zeros: the Squirrel keys 43 nodes, the Serpent 28, the Sparrow 66 per take, so a 2D blend never leaves a bone at whatever the previous clip left it. `idle` = the rest pose exactly (0 nodes move).
4. **No blend-shape curves in any take** (`VESSEL_HUD_AND_ELEMENTS.md:84-90`; `VA.LateUpdate` is the single morph writer): the Squirrel ships 0 shape curves; the Serpent, mantis, Sparrow-Model4 and rhino/urchan rigs carry `Key|…` shape actions pinned at 0 and the Dolphin rig's are pinned at **100** — residue, all of it. `export.strip_shape_curves` (`Tools/Build/vessel_model/export.py`) deletes it.
5. **Frame and units**: pose about WORLD axes of the modelling frame and conjugate per bone (`Tools/Build/vessel_model/takes.py:world_to_bone`: `R_local = R_rest⁻¹ R_world R_rest`), because artists' bones point anywhere (the Squirrel's `bbone_*` all along +X, some rolled 90°; the Serpent's `HeadRootBone` rest euler (0, 150, −90) in bpy). Scene fps 24 (Squirrel, mantis: FBX TimeMode 11) or 25 (Serpent, Sparrow: TimeMode 10) — a pose has no duration so either works; a motion is baked at the scene fps.
6. **Amplitudes must be big enough to survive the controller**: the Squirrel's top tree halves everything (§2.2); every tree saturates at half stick; the Sparrow's and Serpent's whole-hull leans are 24-30°.
7. **Bound-parameter semantics the modeller must honour** (from the trees and drivers): `pitchup` plays on a nose-UP command (Squirrel: `Pitch = −2·YSum > 0` at (0,+1); Sparrow/Serpent: `Pitch < 0` at (0,−1) ⇒ `PitchUp`/`UpAnim`); `yawright` on a nose-RIGHT command; `rollleft` on `YDiff > 0` (= the hull banks LEFT); `speedup` is the CRUISE pose on a two-stick hull (Throttle rests at 1.0) and `speeddown` is unreachable in flight; `Speed+*` clips play only while `Boost` (Sparrow, `hasBoost 1`).
8. **The nine standard poses on a generic hull** (control surfaces = fans/wings/pods; tail = any trailing chain; amplitudes are the shipped ones):

| pose | what a good pose does | shipped amplitudes to match |
|---|---|---|
| `idle` | rest, every bone keyed at zero | Squirrel/Serpent/Sparrow idle: 0 nodes move |
| `pitchup` | the hull (or its root bone) leans nose-UP; the TAIL/aft surfaces go DOWN; forward pods tilt with their tops aft; head features tilt the opposite way to the tail | Serpent root +30°; Manta rig fusal 24.2°; Squirrel tail chain 6.5→26° cumulative over 9 segments, wings/fan pieces 16° (32° compounded), pods 14°, helmet 10°, ears 14°; Sparrow fan roots 16° (pieces 32°, the far piece rises 1.1-1.4 u), blades 24° |
| `pitchdown` | the exact sign mirror | same magnitudes |
| `yawleft` | the hull yaws LEFT (nose to −X) with a bank left-side-down; the tail bends about the up axis so its tip goes to the LEFT side (the body curves into a C around the turn); fans/wings swing about the up axis the same way as the tail; the head counter-turns | Serpent 25° yaw + 25° bank; Manta rig 20.9°; Squirrel tail 8° (WholeTail) → 38° (tip, cumulative), wings 14° (28° at the pieces), helmet −9°; Sparrow fan roots 17° (34° by piece .008), blades 22°, inside fan folds progressively |
| `yawright` | mirror | |
| `rollleft` | the hull rolls LEFT (left wing down) OR the wings twist differentially about their spans so the `.L` fan drops and the `.R` fan rises; the tail twists about the long axis | Manta rig fusal 48.7°, tips ∓1.19 u; Squirrel `WholeWing.L` −29.9° / `.R` +29.9° (pieces 30°), fan tip ∓0.7-0.9 u, tail tip 28° |
| `rollright` | mirror | |
| `speedup` | streamline: fold the fans/wings AFT about the up axis and inboard, slide pods/blades forward, flatten the tail | Squirrel wings 17-18° (36° at the pieces, 0.5-0.7 u), pods +0.16 u; Sparrow fan roots 22° (44° by .008, pieces stack to ~165°, far piece 1.9 u aft), blades +0.07 u forward; Manta rig wings 24° (tips 0.48 inboard / 0.46 aft); Serpent telescopes the stern 3.57 + 1.09 file units aft |
| `speeddown` | airbrake: spread the fans/wings FORWARD and outboard, pull pods back, flare the tail | Squirrel wings 13-15° (31° at the pieces), pods −0.12 u; Sparrow fan roots 21° (42° by .008, far piece 1.8 u outboard/forward); Manta rig wings 35° forward (tips 0.92 forward) |

9. **Binding a new model to an existing controller**: keep the armature name and the take names ⇒ zero controller/`.meta` edits; otherwise the controller's `m_Motion` ids must be re-pointed and the `.meta` regenerated in the editor. For a NEW controller copy the Serpent's shape (Direct → one FreeformCartesian2D Yaw/Pitch tree with the four poses at radius 1, `Blend` default 1) and avoid the Squirrel's x/y top tree (half-weight trap) and the Manta's `Blend` default 0.
10. **Verify offline**: `PROBE --takes` (take count, 1 key / 0.000 s per pose, "0 blend-shape curves", node counts), then `V2` for the signed pose-bone values and the world effect, then compare against the table in item 8.

---

## 5. Missile / boost / special takes (summary)

- Sparrow `Missile Launch 1` (right bay by name = pilot's LEFT bay by position) / `Missile Launch 2`: §2.4 row; additive layer 1, weight pulsed 0→1→0 by `SparrowAnimationController`, state speed 2.5.
- Sparrow Boost: `Boost` bool ⇒ `SparrowSpeedBlendTree` (`Speed+PitchUp/Down`, `Speed+TurnLeft/Right` = the flight poses + 0.25-unit gun-pod slide), 0.998 s cross-fade each way; the orphan `SpeedUp/SpeedDown` clips (fans fold 22°/spread 21°) are never played.
- Serpent `SerpentSpeedIncreaseAnim`: the telescoping stern (§2.3), imported, unbound.
- Squirrel `speedup`/`speeddown`: bound, but `speedup` is the cruise pose and `speeddown` unreachable (§2.2).
- Manta rig `cut`: a stub (`WingThold.L` 6.8°), unbound; Dolphin rig `jawcharge_dolphin` (jaws 46.7°/46.2°, superseded by the 4.76-23.43° code gape) and `startcharge_dolphin` (wings translate 3.69), unbound.

---

## 6. Where the docs/dossiers and the assets disagree (asset is the record)

1. Squirrel dossier §3.2 labels `m_BlendType 2` "SimpleDirectional2D"; Sparrow dossier §3.2 labels `m_BlendType 3` "2D Freeform Directional". YAML 2 = FreeformDirectional2D, 3 = FreeformCartesian2D (§2.1).
2. Squirrel dossier §1.4 / §3.5 assume Unity = (file x, file z, file y) and read `yawleft` as "tail whips OUT to the right"; `V2` on the same file shows the tail tip moving toward the `.L` side, and the identical-header rule measured on the Sparrow puts the tail chain at the flown nose. Unresolved offline; editor check in the frame table.
3. Sparrow dossier §3.4 says `Speed+PitchUp/Down` have the fans folded 58→169°; the file's `Speed+Pitch*` clips are byte-identical to `Pitch*` on every fan bone plus the four gun pods (`V2`, `PROBE` 28 = 24 + 4 nodes).
4. Sparrow dossier says every flight take carries 4 blend-shape curves; this `PROBE` run reports 0 for every SparrowModel1 take (bpy shows no `Key|` actions on Model1, and four on Model4's launches). Serpent: `PROBE` says 0, bpy shows four `Key|SerpentArmature|…` actions pinned at 0 — the probe and bpy count shape residue differently; export none either way.
5. `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md:83` ("`MantaAnimationContoller` → Animator floats … keep binding") describes the Squirrel and Serpent; the Manta prefab itself runs `MantaAnimation` code puppetry and its controller is on an inactive GO (§2.5).
6. `VA:455` comment "The current fleet's takes are clean" is false for the Serpent, mantis, Dolphin, Rhino, Urchin files (shape residue, §4.4).
7. `.claude/skills/vessel-model/references/CONTRACT.md` §5 says the Serpent's takes are "poses" (true) and the Sparrow's "1-2 frame poses" (true) but lists the Manta as "code + an Animator on the mantis rig's clips" without noting the Animator is inactive and `Blend` is 0.
