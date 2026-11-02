# Meerkat: the wormhole vessel's own hull

> A NEW hull on a BORROWED rig (2026-11-03), built by `/vessel-model` from `Tools/Build/vessel_model/specs/meerkat.py`.
> There is no artist file. Since it was cloned (2026-10-27) the Meerkat (`VesselClassType.Meerkat = 28`) flew in the
> Stoat's model: `Meerkat.prefab` nests `Stoat.fbx` under its `StoatHull`, and `StoatAnimation` lopes it. This file
> keeps every name the prefab and `StoatAnimation` reach, so the prefab binds it by a guid swap alone (PIPELINE "The
> borrowed-rig path", LESSONS 48, the Goby's case against the Stoat). Yash asked for it.

## Brief

The vessel (`Docs/Reports/MEERKAT.md` section 1, `Assets/_Scripts/Controller/Vessel/R_VesselActions/MEERKAT.md` section 10):
- RT forms a wormhole (the soliton) on its nose, and it dives through.
- Y (Sentinel Mode) leaves sentinels at each wormhole's way in. A sentinel is an afterimage of THIS hull, baked from its
  renderers and stood up "like a real sentinel meerkat": nose to the sky, back to the way in (`MeerkatSentinels.BakeGhost`).
- Its passive (Exit Portal) lays six prisms on a hexagon round the way out.

The look: a spaceship inspired by a meerkat, never the animal (Garrett, 2026-10-18), and not the Stoat recoloured.
- The Stoat is a low prow over a shingled spine with dorsal fins.
- The Meerkat has an alert head raised on a neck over a smooth body, a banded back, digging claws under the chest and a
  long tapering tail.

The camera sees it from straight behind and level: `MeerkatCameraSettingsSO` followOffset (0, 0, -21), 68 degree FOV,
look-ahead 40, root scale 0.8. The hull is about 30 x 40 px at 1080p there. What reads at that size:
- the raised head with its ear dishes, standing over the back;
- the two ember mouths under their domain haunch cowls;
- the banded back, as the lope pitches it;
- the legs below, like a small creature standing.

The visor, the face and the claws are for the hangar, the cards and the standing sentinels.

| clade | ship |
|---|---|
| alert raised head | **the sensor module** (`nose`): a wedge, broad between the ears, tapering to a short blunt snout, raised on a neck column with a domain collar, floating over it |
| dark eye band | **the visor**: a band of sapphire glass across the nose bridge, slanting back and down both sides, on a hairline seam with dark edges (Window) |
| pale face, muzzle | a domain **crest** plate on the brow behind the visor, a domain **muzzle** plate ahead of it |
| small dark ears | two **sensor dishes** low on the back of the head, dark faces in domain rims |
| dark nose | **the soliton former**: a dark ring floating round the snout, its inner wall and the nose tip hot (Engine), where RT forms the wormhole |
| slender body | a smooth, pear-shaped **core** (a narrow chest, a fuller belly), skinned along the five plate bones, so the lope bends it in one line |
| banded back | a dark **carapace** plate per plate bone over the core, its edges chevrons pointing forward, each carrying a domain **chevron stripe** across its rear third |
| digging fore-paws | two **digging arms** (`leg.1.L` / `leg.1.R`): a shoulder ball with a domain cap, a forearm, an armoured paw with a domain knuckle, three hooked claw blades with hot tips. The lope's landing swings them forward: the dig stroke |
| hind feet | two **haunch legs** (`leg.2.L` / `leg.2.R`): a thigh with a domain top and a flat foot pad with a domain toe and a thruster vent. Standing, they and the tail are the sentinel's tripod |
| haunches | twin engine **nacelles** at the hips under domain haunch **cowls**; their ember mouths are where the prefab's two VesselJets sit |
| long thin tail | four telescoping dark **sleeves**, domain collars at their joints, rising a little toward the tip, and the dark **tip** |

## Rig contract (the Stoat's, which `Meerkat.prefab` and `StoatAnimation` bind)

- **File**: `Assets/_Models/Vessel Models/Meerkat.fbx`, guid `81b9ac1e02714f0fa49b1eef2bb2d9f5`. The `.meta` is
  `Stoat.fbx.meta` byte for byte with the guid changed, so it remaps all four slots by name: Body, Domain, Engine and
  Window go to the fleet's shared materials. Preset `zup`.
- **Nodes**: mesh `mesh`, geometry `mesh`, armature `rig` (the preset's R(0,0,180), S 1).
- **Rig**: the Stoat's armature, imported from `Stoat.fbx` at commit `e3653c8243` (pinned in the spec, so a later Stoat
  edit never moves this file).
  - The 15 bones: `hull` (the core plate), `plate.0`, `plate.1`, `plate.3`, `plate.4`, `nose`, `tail` .. `tail.5`,
    `leg.1.L/R`, `leg.2.L/R`.
  - Every bone, the mesh node and the armature rest where the Stoat's do (`rest_compare.py`, below).
  - `StoatAnimation` resolves the bones by name under the hull transform and poses each from its rest. So the lope, the
    turn bend, the head nod, the tail swing and the leg reach move this hull as they moved the Stoat's.
- **Frame**: Blender nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y).
- **Jets**: the prefab's two VesselJets ride `StoatHull`, not a bone, at Unity (+-0.3, -0.05, -2.15) = Blender
  (-+0.3, 2.15, -0.05). The nacelle lips end at Blender y 2.13, just ahead of them.
- **Size**, against the Stoat's drawn hull (Blender frame):

  | axis | Stoat | Meerkat | change |
  |---|---|---|---|
  | x (width) | -0.447 .. 0.447 (0.894) | -0.465 .. 0.465 (0.930) | +4.0 % |
  | y (length) | -3.168 .. 4.820 (7.988) | -3.166 .. 4.820 (7.986) | -0.03 % |
  | z (height) | -0.665 .. 0.769 (1.433) | -0.658 .. 0.790 (1.447) | +1.0 % |
  | diagonal | 8.164 | 8.169 | +0.06 % |

  The widest point is the haunch cowls, inside the 1.2-wide hit box. The hit boxes (1.2 x 1.4 x 7 and 1.6 x 1.8 x 7.6),
  the skimmer and the camera were tuned to this envelope.
- **Slots** (polygons): 0 Domain 1,382; 1 Body 5,252; 2 Window 66 (the visor); 3 Engine 340 (nacelle mouths, the
  former, claw tips, foot vents). By area: domain 10 %, window 0.4 %, engine 1.0 %.
- **Shape keys** (Charge, Mass, Space, Time: the Stoat's names and order), as share of the diagonal:

  | shape | ability it shows | what moves | Meerkat | Stoat |
  |---|---|---|---|---|
  | Charge | Sentinel Mode, the zap | the claw blades lengthen into long diggers; the ear dishes rise and flare; the former's ring flares | 4.3 % | 7.5 % |
  | Mass | Exit Portal, volume | the core and carapace fatten about the spine and the belly drops; the haunches swell; the arms and legs ride out with the body's surface | 3.7 % | 4.5 % |
  | Space | the Soliton, reach | the neck telescopes 0.32 and the head reaches up and forward; the tail draws out 17 % | 5.9 % | 10.5 % |
  | Time | Faster Ride, rate | the snout sharpens; the ears and the carapace rake back; the claws fold under the chest | 3.6 % | 5.3 % |
- **Takes**: `rig|rest` first (Unity imports the rest pose from the first take, LESSONS 51), then the Stoat's four
  documentation poses retargeted bone for bone: `arch`, `land`, `turnleft`, `turnright`. Single frame, no blend-shape
  curves. `StoatAnimation` drives the bones in code and plays none of them.
- **Budget**: 13,432 triangles, 15 bones, one skinned mesh, four shapes.

## Measured (2026-11-03, on Windows with bpy 5.2.3)

| check | result |
|---|---|
| `build.py meerkat` (in place) | verified by two of the three readers: the repo codec + probe, and the Blender re-import. **The assimp reader did not run: assimp is not installed on this machine.** Every other verify check passed, including the `.meta`'s four material remaps and the rest take leading |
| `build.py meerkat --check` | OK: the spec rebuilds to the committed file's structure (assimp again not run) |
| `rest_compare.py <Stoat.fbx @ e3653c8243> Meerkat.fbx` | OK, Blender reader: 17 shared nodes, 0 only in either, every bone, the mesh node and the armature at 0.0000 units / 0.00 deg. assimp reader not available |
| take parity against the Stoat (`take_parity.compare` + `compare_moving_nodes`) | 5 takes, 39 moving part-takes, 0 differences; FBX moving-node sets identical; materials identical. Morph parity differs on 33 of 42 part-morphs, by design: the geometry and its morphs are new |
| `assembly_report.py Meerkat.fbx --floating <FLOATING>` | OK: 11 / 11 parts float, gaps 0.62-1.44 %, median 0.79 %. The body chain (`plate.0+plate.1+hull+plate.3+plate.4`) is one part |
| `pose_clearance.py --parts nose,leg.*,tail* --takes all` | OK: tightest 0.44 % (the tail sleeves in a full turn) |
| blended poses, rotations composed AND lifts summed, the way `StoatAnimation` plays them (`land+turn*`, `arch+turn*`) | every part clear, tightest 0.44 %. The Stoat's own hull has 35 parts under 0.4 % on the same check |
| `pose_clearance.py --combos` (rotations only) | reports `leg.1` at 0.36 % in `land+turn*`: it zeroes each bone's location, so the landing's raised chest is missing. That pose is not one the game plays (LESSONS 55) |
| `measure_vessel_models.py --no-refs --only Meerkat` | 4 element shapes, all real; extent 0.930 x 7.986 x 1.447 |
| `style_metrics.py --fleet` (informative) | smooth 0.62, hard 0.15, domain 0.10, window 0.004, engine 0.010, 25 domain regions, 53.6 tris per unit area: inside every artist band (the window at its floor, as the Dolphin's) |

Sheet: `sheets/meerkat_after.jpg` (the pipeline's contact sheet: four views, the four morphs, the four lope / turn poses).

## Rounds

1. A smooth core with five thick domain bands, a small round head on a thin neck, a striped tail. It read as a bee or a
   striped caterpillar, the head as a ball on a stick, and the tail as a dashed line.
2. A wedge head with a slanted visor across the nose bridge, thinner chevron bands, pale tail tops. It read as a fire
   salamander with a duck's bill. A pale ring at the nose read as a pig's snout. The head barely cleared the back.
3. The body lowered 6 cm under the bones' line so the head stands clear; a rounder head with a short blunt snout; bigger
   ear dishes; a dark former ring; dark tail sleeves with domain collars. The standing sentinel read well (head on top,
   paws held before the chest, the tail as the prop), but the body was a featureless tube with clip-like stripes.
4. A dark carapace plate per plate bone with a domain chevron on each. The tube became a plated, banded back; the crest
   narrowed so it no longer reads as a cap.
5. The triangle budget (15.7k down to 13.4k) and the blended poses: the tail root set behind the nacelle lips, the neck
   rising 4 cm ahead of the chest, the paws 1.2 cm further out. That took the blended clearance from 4 near-touches to
   none.

## What only the owner's eye can judge

- Whether it reads as a meerkat in flight, and not a mongoose or a salamander. The length is the Stoat's (8 units, about
  6.7 times its width), so the body stays long. The head, the visor band, the ears and the claws carry the meerkat.
- The standing sentinel in the game, baked mid-lope with its morphs: whether the head, the paws before the chest and the
  tail prop read as a sentinel meerkat at the wormhole's way in.
- The colours on the real materials. Domain is 10 % of the area, at the low end of the band. The window is a thin band;
  whether the visor reads on the game's sapphire `ScreenVesselMaterial`.
- The Body material is transparent without depth writes (`COLOR.md` section 6). The visor, crest, carapace and stripes
  float on 0.4-1.6 cm seams over dark shells, so a visible rim could show overlap the clay renders cannot.
- The morphs at play speed. Charge and Time are modest (3.6-4.3 %, against the Stoat's 5-7.5 %), and the Space reach
  lifts the head to about 0.83 at full Space.

## Not done here

- **The guid swap in `Meerkat.prefab`.** Thirteen references to `0ef0b84d93774ac39ddc699c59d0821a`, all in the model
  instance under `StoatHull`: its `m_SourcePrefab`, the twelve modifications on the model root (fileIDs
  `919132149155446097` / `-8679921383154817045`, the same for every model) and the stripped root Transform. They become
  `81b9ac1e02714f0fa49b1eef2bb2d9f5`. The root's `m_Name` override reads `Stoat`; nothing finds it by that name
  (`StoatAnimation` takes the renderer under the hull transform).
- **The same swap in `Tools/Build/author_meerkat_assets.py`'s idempotent edits.** Its clone of `Stoat.prefab` is a spent
  one-shot, so `--check` does not pin the model guid today. A from-scratch re-clone would bring the Stoat's model back.
- **The class card** (`render_cards.py Meerkat`, registered in `VESSELS`), and the Meerkat's own ability icons
  (`/ability-icon`).
- **The editor steps** after the swap:
  - reimport;
  - `FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs`;
  - `Audit Vessel Construction`;
  - `Audit Vessel Tails and Jets`;
  - a flight in the Toybox Vessel Changer;
  - Y in Burrow Run, to see a sentinel stand.

  These belong in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. There are no crystal hull-fusion bakes for the Stoat or the
  Meerkat.
