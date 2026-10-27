# Grizzly: the bear

> A NEW hull (2026-10-27), built by `/vessel-model` from `Tools/Build/vessel_model/specs/grizzly.py`. There is no artist
> file. Until now the Grizzly flew the generic wedge, `Vessel_Wedge_Scene (4).fbx`: eight static part meshes, no
> armature, no element shapes, and jet pods wearing Lime. That file is shared with `TimeDandruff.prefab` and
> `ExplodableProjectile.prefab` (`Docs/VESSEL_CONSTRUCTION.md` 7.2), so it was left untouched. The Grizzly stops
> drawing it. Sheets: `sheets/grizzly_after.jpg` (the contact sheet) and `sheets/grizzly_views.jpg` (the game's
> straight-behind camera at its distance, the rear, the side, three-quarter and hangar views).

## Brief

Garrett, 2026-10-27: "make a new much more awesome model for the Grizzly." It is a spaceship inspired by a grizzly bear
(Ursus arctos horribilis), never the animal. The Grizzly is the fleet's brawler and artillery piece: charged cannon,
sniper scope, trigger bombs, plasma claws, Rush and Dig In. Its camera sits straight BEHIND it
(`GrizzlyCameraSettingsSO` followOffset (0, 0, -60), dynamic 30-100), so the hull must read as a bear from behind by
its outline: the shoulder hump over everything, four heavy haunch engines, and forelimbs reaching ahead with their
claws.

| clade | ship |
|---|---|
| shoulder hump | **the reactor shell** (`hump`): a thick armour dome floating over the shoulders, a domain saddle down its ridge, vent seams on its flanks |
| head | **the command module** (`head`), floating ahead of the torso across a neck gap: a raised brow carrying the sunk cockpit canopy, two angled sensor windows (the eyes) at the stop |
| snout | **the cannon barrel**: a deep, blunt muzzle (a long flat one read as a crocodile in round 1); the charged shot and the sniper leave along it, the tip is the ember bore, and a conformal domain blaze runs down the ridge |
| jaw | a hinged lower **mandible** (`jaw`) under the barrel, two fang-like heat sinks at its lip |
| round ears | two **sensor dishes** (`ear.L` / `ear.R`): a domain rim round a glass lens, lifted clear of the brow by measurement (`lift_clear`) |
| forelimbs | shoulder **pauldrons** (`shoulder.L` / `shoulder.R`, a conformal domain cap on each) and **claw gauntlets** (`claw.L` / `claw.R`): an armoured paw with a conformal domain knuckle plate and four hooked claw blades whose tips glow (Engine: danger). This is where the plasma claws come from |
| hind legs, haunches | the four **thruster pods** (`jet.T.L`, `jet.T.R`, `jet.B.L`, `jet.B.R`): a wide hip, an intake groove, a thigh tapering to a domain collar, then a bell whose dark bezel sinks to an ember throat round a dark spike. An armour cowl sits on each pod's outboard side |
| belly, digging | **the dig-in anchor** (`keel`): a three-tined spade floating under the torso |
| rump | the torso's stern: a domain rim round a dark recess with a small ember reactor port |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Grizzly.fbx`, guid `22782b7e8ad84aa98596c08cd98e93bb`. The `.meta` is
  Thresher's with a new guid: it remaps Domain, Body, Window and Engine to `GreenAccentVesselMaterial`,
  `BlueBaseVesselMaterial`, `ScreenVesselMaterial` and `EngineVesselMaterial`. Preset `zup`.
- **Nodes**: mesh `mesh` (identity, metres), armature `rig` (the preset's R(0,0,180), S 1); root bone `hull`.
- **Frame**: Blender nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): nose +Z, up +Y, pilot's
  left -X.
- **Bones** (15): `hull`; `head` -> `jaw`, `ear.L`, `ear.R`; `hump`; `keel`; `shoulder.L/.R`; `claw.L/.R`;
  `jet.T.L`, `jet.T.R`, `jet.B.L`, `jet.B.R`. Rigid skinning, one bone per part.
- **The jets**: each pod's mouth is at the prefab's VesselJet, Unity (+-2.725, +-1.461, -3.81) on `Ship_Wedge_Body`.
  `.L` is the pilot's left (Unity -X), so `jet.T.L` is `Ship_Wedge_Jet_UL`.
- **Size**: 7.6 wide (the claws), 10.6 long (snout tip to jet mouths, against the wedge's 11.1), 6.0 tall. The
  wedge's two fins made it 11 tall; the bear has none (declared in the spec's `size_tol`).
- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine. **Shapes** (travel as % of the 14.3 diagonal):
  - **Charge** 8.4 %: the claw blades lengthen, and the paws swell and reach forward (the weapon).
  - **Mass** 5.5 %: the torso and the reactor shell bulk out, and every floating part rides out with them.
  - **Space** 6.8 %: the barrel telescopes forward (the sniper's reach), and the dishes rise.
  - **Time** 7.6 %: the four pods stretch aft, their bells flare, and the reactor shell sweeps back.
- **Takes**: `rest` (first, the build adds it), `dig` (the anchor plunges 1.3 units and rakes forward, and the claws dig down: the Dig In icon is this take), `roar` (the
  jaw drops, the head lifts and the ears tip back), `swipe` (the right claw rakes down and in). These are single-frame
  poses with no blend-shape curves. No code plays them yet.
- **Budget**: 13,920 triangles, 7,028 vertices (the convention's ceiling is 14k).
- **Hovering assembly**: `assembly_report.py Grizzly.fbx --floating head,jaw,ear.L,ear.R,hump,keel,shoulder.L,shoulder.R,claw.L,claw.R,jet.T.L,jet.T.R,jet.B.L,jet.B.R`
  reports 15 / 15 parts floating, median gap 0.79 % (the tightest 0.68 %, the claws 4.58 %).

## How the prefab draws it

`Grizzly.prefab` keeps the wedge's part transforms: `Ship_Wedge_Body` (`Fusilage`), the four `Ship_Wedge_Jet_*`, the
two fins and `Ship_Wedge_Projectile`. They keep their colliders, rigidbodies, VesselJets and guns. Only their eight
MeshFilter / MeshRenderer pairs went. The model is nested at `Ship_Wedge_Body/GrizzlyHull/Grizzly`, so it leans with
the fuselage. `VesselCustomization._shipGeometries` names `GrizzlyHull`, which resolves to the model's
SkinnedMeshRenderer, and `_domainReplacesMaterials = [GreenAccentVesselMaterial]` repaints the Domain slot by identity.

`BufoAnimation` still turns the wedge's parts, and the drawn bones follow them each frame (the `RhinoAnimation`
pattern):
- **the four pods follow their thrusters rigidly**, so a pod's mouth stays on its VesselJet;
- **the reactor shell and the anchor follow the fins at `humpFollow` / `keelFollow` = 0.35**, about their own bases.
  The fins swing about the hull's centre, so a rigid follow would slide the shell off the shoulders; this way the
  shoulders roll.
- `SkinnedMeshRenderer` is found at runtime, so `FlareEngine` / `FlareBody` light the model's Engine and Domain slots.
  Culling is off for it (`updateWhenOffscreen`), because the pods swing past the bind bounds.

The element morphs are found by name (`VesselAnimation.CollectElementShapes`). The Grizzly had none before this, so its
elements now show on the hull for the first time.

## Not changed, and what it costs

- **Colliders** are where the wedge's were. The two fin boxes (Unity y +-3.7, 3.6 tall) reach higher and lower than
  the reactor shell and the anchor; the claws reach wider than any box. Gameplay is unchanged. Whether to re-fit the
  boxes to the bear is a tuning call for the Grizzly's owner.
- **Crystal hull fusion**: the four Grizzly bakes were solved for the wedge's mesh. They were dropped
  (`CrystalHullFusionConfig` entries `bake: {fileID: 0}`; the stale assets were deleted). The fusion now solves on a
  worker at runtime and warns once, until **FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions** is run in
  the Editor.
- **The plasma claws** (jtgjones, `grizzly-v3`) root at `GrizzlyFlamethrowerActionSO` sideOffset 6 / noseOffset 4,
  which is outboard of the new gauntlets. The gauntlets' claw tips sit at about Unity (+-3.7, -1.3, 5.7) on `Ship_Wedge_Body`. Moving the
  muzzles onto the claws is jtgjones's call, on their branch.
- `Ship_Wedge_Scene (4)` (an empty GameObject at the prefab root) is left as it was.

## Rounds

| round | what the sheet said | change |
|---|---|---|
| 1 | the hump and its saddle read; the head read as a crocodile (long flat snout); paws tiny and spindly; pods were barrels with fully orange discs; 23.5k tris | - |
| 2 | nozzles read as nozzles; ears and claws read as a bear from the front | a bezel, an ember throat and a spike in each mouth; bigger paws and claws; a brow rising off a shorter snout; a rounded hump; thicker pauldrons |
| 3 | bear from front and side; stern port smaller; the domain on the pauldrons, paws, jaw and flank showed stair edges | a deeper, blunter muzzle; a dark stern recess round a small ember port; fewer stations |
| 4 | clean domain edges | the blaze, pauldron caps and knuckle plates became CONFORMAL plates (`offset_shell`) on hairline seams, the Orca's lesson |
| 5 | 15 / 15 parts float; 13.9k tris | thinner lofts; the ear dishes lifted until clear of the brow, by measurement |

## Supporting resources

| resource | where | made by |
|---|---|---|
| class card, inactive, silhouette, squad and hangar preview images | `Assets/_Graphics/CardImages/Grizzly*.png`, `Design Assests/Hangar/Grizzly_Hangar_Preview.png`, wired into `SO_Class_Grizzly` (the legacy BUFO icons retired) | `vessel_model/render_cards.py Grizzly` |
| the four ability-row icons (Dig In, Weapon Systems, Charged Cannon, Rush) | `Assets/_Graphics/Icons/AbilityIcons/Grizzly/`, bound in the embedded GrizzlyHUD in place of four `*-PLACEHOLDER.png` sprites; sheet `Docs/Icons/sheets/grizzly.png` | `icon_kit/author.py grizzly` (`icon_kit/specs/grizzly.py`) |
| Hangar list card art | waits on the roster: the Grizzly is not in `SO_Classlist_Classes` (the Hangar list), so `author_hangar_card_art.py` (which has its sheet) makes no card until it is added there | `author_hangar_card_art.py` |
| crystal hull fusion bakes, the codex's Vessels variant image | Editor-only bakers | `Docs/UNITY_VERIFICATION_CHECKLIST.md` |

## Gates

```
python3 Tools/Build/vessel_model/build.py grizzly --check            # drift against the committed file
python3 Tools/Build/vessel_model/assembly_report.py "Assets/_Models/Vessel Models/Grizzly.fbx" --floating <the 14 parts above>
python3 Tools/Build/vessel_model/render_cards.py Grizzly --check      # the five card images
python3 Tools/Build/measure_vessel_models.py --only Grizzly           # 4 labelled shapes, 4 that move; 1 reference
```

Not run here: the Editor import, the audits (Audit Vessel Elemental Morphs / Construction / Tails and Jets), the
fusion bake and a flight. These are listed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
