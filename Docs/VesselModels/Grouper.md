# Grouper: the Wrasse's carrier hull

> A NEW hull (2026-10-20), built by `/vessel-model` from `Tools/Build/vessel_model/specs/grouper.py`
> to the fleet convention (`.claude/skills/vessel-model/references/FLEET_CONVENTION.md` 1-4). There is no
> artist file. `Wrasse.prefab` draws it (2026-10-20, `author_wrasse_assets.py` `bodies_retrofit`); the Sparrow's
> mesh renderer is off, its bones, jets and colliders stay.
> The mode that flies it is `Assets/_Scripts/Controller/Arcade/WRASSERUN.md`.

## Brief

Garrett, 2026-10-20: "lets use the new model building capabilities to give the wrasse and grouper proper 3d vessels
with animations etc." The ship is inspired by the grouper, a big-mouthed, deep-bodied reef predator, and is never the
animal itself:

| clade | ship |
|---|---|
| huge mouth | **ram scoop**: an oval intake ring on the face with a glowing throat and dark intake vanes, over a hinged lower mandible (`jaw`) that juts past the lip (the underbite), with a domain lip trim |
| spiny dorsal | **sensor / heat-sink crest** (`dorsal`): nine domain fins with dark leading spars on a spine rail. They overlap into one serrated crest, tallest at the front, with a glowing sensor tip on every other fin and a soft rear vane |
| pectoral fins | **stubby wings** (`fin.L` / `fin.R`) with sunk domain panels and a **thruster pod** at each tip: dark intake, domain band, glowing mouth aft, lime nav light |
| caudal fin | **vectoring tail**: a boom (`tail.1`) behind a joint collar, then a rounded vane (`tail.2`) with a sunk domain panel and two vector nozzles |
| eye | **canopy**: a glass bubble on the forehead in a dark frame, plus a sensor eye in a bezel high on each cheek |
| gill cover | an armoured operculum plate per side and four glowing vent louvres behind it |
| deep body | a tall, laterally pressed gunmetal hull. Each flank carries two sunk domain plates, split by a dark lateral seam. Domain saddles sit either side of the crest rail |
| pelvic / anal fins | two ventral skegs and an anal stabiliser (Body, with a domain edge) |

Two main engines flank the peduncle with their mouths aft (Unity about (+-0.78, -0.30, -2.62)).

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Grouper.fbx`, guid `e8c932601ef7e35b1f3017557298424c`.
  - The `.meta` is Thresher's with the guid changed. It remaps all four slots: Body `54e78ce1…`, Domain `539a8c65…`,
    Engine `0c127fff…`, Window `923d3665…`.
  - Preset `zup`: the nose lands at Unity +Z and up at +Y with the model root at identity, as `Thresher.fbx` does in
    `Thresher.prefab`.
- **Nodes**: mesh `mesh` and armature `rig`, siblings at the top of the file; root bone `hull`.
- **Bones and rest heads** in Unity space (x right, y up, z forward, root-relative), skinned rigidly with one bone per
  part:

| bone | parent | head (Unity) | skins |
|---|---|---|---|
| `hull` | — | (0, 0, 0) | 4294 verts |
| `jaw` | hull | (0, -0.62, 4.30) | 168 |
| `fin.L` | hull | (-1.50, -0.55, 2.05) | 410 |
| `fin.R` | hull | (1.50, -0.55, 2.05) | 410 |
| `dorsal` | hull | (0, 2.01, 3.20) | 1060 |
| `tail.1` | hull | (0, 0.10, -2.30) | 180 |
| `tail.2` | tail.1 | (0, 0.10, -4.05) | 372 |

- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine. Polygons per slot: 1173 / 4776 / 544 / 996.
- **Shape keys**, as travel in % of the 18.1 diagonal:

| key | meaning | what moves | travel |
|---|---|---|---|
| Charge | weapon | the crest stands 1.6× taller; the ram scoop juts forward and drops open | 4.5% |
| Mass | volume | the hull, jaw and boom swell; the pods swell 1.45×; the wings ride out | 4.5% |
| Space | reach | the wings reach 1.3× along their span; the pods ride out; the boom telescopes | 9.1% |
| Time | speed | the wings sweep back; the crest rakes back; the vane spreads and lengthens | 9.3% |

- **Takes**: none drive it yet. The code poses the bones by name. Four documentation poses for the sheet and the
  studio, each held by `pose_expect` on the re-import:
  - `gape`: the jaw drops open, +24° about X.
  - `beatleft` / `beatright`: the boom and vane vector to each side.
  - `flare`: the wings brake down 35°.

## Measured (`fbx_probe.py`, Unity units)

| | Grouper | the Sparrow it replaces in Wrasse.prefab |
|---|---|---|
| span (x) | **11.90** (-5.95 .. 5.95) | 12.50 (−4.8%) |
| length (z) | **12.43** (-5.95 aft .. +6.48 jaw tip) | 12.23 (+1.6%) |
| height (y) | **5.62** (-2.24 skegs .. +3.38 crest) | 1.78 (the clade's deep body, declared in `size_tol`) |
| triangles | 13,556 (6,894 verts) | 25,984 |

`build.py grouper --check` reports OK. All three readers pass: the codec, assimp (13,556 faces, 4 animations,
4 materials) and the Blender re-import.

`style_metrics.py --fleet` places it at:
- smooth 0.71, hard 0.10;
- domain 0.20, window 0.030, engine 0.026;
- 27 domain regions.

Smooth sits just above the artist band (0.45-0.69) and hard just below it (0.13-0.23). That makes it a little softer
than the artist hulls. The metric is informative only.

## What the sheet shows (`Docs/VesselModels/sheets/grouper_after.jpg`)

- **Chase:** the camera sees a deep gunmetal hull with the domain flank plates. The serrated crest stands along the
  back, the two stubby wings carry glowing pod mouths, and the vane sits behind the boom.
- **Side:** the read is a grouper. A big blunt head with the scoop and underbite, the tall crest, the deep body tapering
  to the boom, and a rounded tail vane.
- **Morphs:** each one reads at the chase distance (Charge: the crest; Mass: the bulk; Space: the span; Time: the
  sweep).
- **Poses:** the tail vectors to each side and the wings flare. `gape` is small from behind and shows from the front.
- **Rounds:**
  - Round 1: the crest read as a row of needles.
  - Round 2: the fin chords were widened past the fin spacing, so they overlap into one crest, and the vane was
    enlarged.

## Open

- **Integrated by NAME** (2026-10-20): nested under the Wrasse root as `Grouper`, the ship geometry (domain on slot 0),
  its bones puppeted by `WrasseCrewBodies` (tail beat and turn bend, fin scull and bank, jaw and dorsal on boost). Still
  the Sparrow's: the hit box, the jets' bones and the engine flare (`KITE_NAVIGATOR.md` Follow-ups).
- Card images are not rendered from this file yet (see the hand-off report).
- The owner's flight is the result.
