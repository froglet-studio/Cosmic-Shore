# Pistol: the second seat of the Goby and Pistol

> A NEW body (2026-10-25), built by `/vessel-model` from `Tools/Build/vessel_model/specs/pistol.py`. There is no artist
> file. Until now `GobyPistolDuo.BuildView` drew the Pistol as an orange capsule. Its brief is
> `.claude/skills/duo-vessel/references/designs/goby-and-pistol.md`. The Goby's own hull is `Goby.fbx` (`specs/goby.py`).

## Brief

Garrett, 2026-10-24: "now give the pistol a model". A spaceship inspired by a pistol shrimp (Alpheidae), never the
animal. The Pistol seat films it side-on through a near-orthographic lens about 29 units across, so the shrimp fills
about a seventeenth of the screen. The Goby's chase camera sees it from behind and above. The silhouette therefore has
to say "pistol shrimp" side-on.

| clade | ship |
|---|---|
| snapping claw (major chela) | **the cavitation cannon** (`gun`): a barrel housing on an arm link, a fixed lower finger, an Engine muzzle between the fingers, two domain barrel bands |
| the claw's dactyl | **the hammer** (`claw`), cocked 75 degrees open at rest; code snaps it shut (take `snap`). A plunger knob under its root |
| small claw (minor chela) | a slender manipulator (`pincer`) with domain finger tips |
| carapace | a core hull under three overlapping plates (dorsal shield, two side shields) on hairline seams; a domain stripe on the ridge, domain trim on the side shields' lower edges, a short prow |
| transparent eye hoods | the core's front is glass (Window) |
| eye stalks | two sensor masts on top of the head (`eye.L` / `eye.R`), glass tips on domain collars |
| antennae | two long sensor whips (`antenna.L` / `antenna.R`) arcing up and back over the body, radius ramped, capped by a bead; domain whips on dark peduncles |
| walking legs | three pairs of bent landing struts (`leg.1.L` .. `leg.3.R`), each on a thruster foot that glows underneath |
| abdomen | a segmented boom of six telescoping rings on a curved spine, a domain rim on each ring's trailing top, a pleuron plate hanging down each side (the scalloped lower edge side-on); three rings on `tail.1`, three on `tail.2` |
| tail fan | **the vectoring fan**: a nozzle hub with an Engine mouth and the telson (`fan`), two stacked uropod blades per side (`fan.L` / `fan.R`) with domain outer edges, tilted 27 degrees down along the boom's end |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Pistol.fbx`, guid `7d2e9b4c1a6f4e83b5c0d8a2f6e1b937`. The `.meta` is Jack's
  with the guid changed: it remaps the four slots to the fleet's Domain, Body, Window and Engine materials. Preset `zup`.
- **Nodes**: mesh `mesh` (identity, metres) and armature `rig` (the preset's R(0,0,180), S 1); root bone `hull`.
- **Frame**: Blender nose (claw tips) -Y, up +Z, pilot's left +X. In Unity the nose and travel are **+Z**, up is
  **+Y**, and the pilot's right is **+X**. The big claw is on the pilot's right, Unity +X.
- **Camera side**: posed with `LookRotation(travel, planeUp)`, the model's Unity +X faces the Pistol camera, which sits
  on the plane's -M side looking along +M. So the camera sees the claw side.
- **Origin**: on the carapace's centre line between the legs, where the capsule's centre was.
- **Bones** (19): `hull`; `gun` -> `claw`; `pincer`; `eye.L`, `eye.R`; `antenna.L`, `antenna.R`; `leg.1.L` ..
  `leg.3.R`; `tail.1` -> `tail.2` -> `fan` -> `fan.L`, `fan.R`. Rigid skinning, one bone per part.
- **The hammer**: the `claw` bone's head is the knuckle, Unity (0.19, 0.062, 0.615) in the model frame. Shutting it is a
  rotation of +75 degrees about the model's +X axis through that point (`Quaternion.AngleAxis(+75, Vector3.right)` in
  the model frame); the tip moves down and forward. The `snap` take holds exactly that pose.
- **Size**: 1.65 long claw tip to fan tip, 0.56 wide (antenna to antenna), 0.70 tall (struts' feet to the whips' arc).
  The capsule was 2.72 along its travel and 1.7 across (Unity's capsule is 2 tall; scale (2r, 1.6r, 2r), r 0.85).
- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine.
- **Shape keys** (share of the 1.88 diagonal): Charge 6.0 % (the barrel swells, the fingers reach, the hammer rides its
  knuckle); Mass 6.7 % (the carapace bulks out, the rings thicken, the struts splay, every floating part rides the
  hull's swell so the gaps hold); Space 9.9 % (the whips reach further, the eye masts extend); Time 4.5 % (the uropods
  spread and lengthen, the telson draws out).
- **Takes**: `rig|rest` (first, so Unity imports the rest pose), `rig|snap`, `rig|dive` (the tail curls under, the
  struts fold back, the claws tuck), `rig|glide` (the uropods spread, the boom lifts, the struts fold halfway). Single
  frame, no blend-shape curves.

## Measured

| check | result |
|---|---|
| `build.py specs/pistol.py --check` | OK; codec, assimp and Blender re-import all pass |
| `assembly_report.py Pistol.fbx --floating` (all 18 non-hull parts) | OK: 19 / 19 parts float, gaps 1.07-2.05 %, median 1.64 % |
| triangles / bones | 11,588 / 19 |
| `style_metrics.py --fleet` | smooth 0.59, hard 0.15, domain 0.13, window 0.022, engine 0.009, 21 domain regions, 18.5 tris per unit area: inside every fleet band |

Sheets: `sheets/pistol_after.jpg` (the pipeline's contact sheet) and `sheets/pistol_camera_side.jpg` (the camera's side
at rest and in `snap`).

## Rounds

1. A straight lofted tube with flat rings, an egg-shaped claw with a horn for a hammer, and vertical struts. From the
   camera's side it read as a wasp, and the banded rings were too loud.
2. The carapace shorter and humped; the boom on a curved spine with pleuron plates; the struts bent so they read as
   legs side-on; a boxier, longer barrel; a stout hammer cocked 75 degrees with a plunger, shut fully by `snap`. It
   reads as a pistol shrimp side-on and at game size.
3. The barrel's bands never painted: its loft had stations 0.09 apart, and a band paints only faces whose centre falls
   in it, so the loft is now dense. Taller side-shield trim, a longer fan, the uropods moved out (0.58 % to 1.07 %).

## Not done here

The main session wires it: `GobyPistolDuo` still draws the capsule, so the swap, the scale against the capsule and a
look in the Editor belong to that change.
