# The Ray, start to completion: the worked example

Read this when a step of `/fauna-maker` is unclear. It is the one creature that has been taken the whole way: from
"give us a manta" to four element prefabs in Unity. Every step names the file to read and the command that proved it.
Where the first attempt was wrong, the wrong attempt is here too, because that is what the checks are for.

Status (2026-11-17): everything below is checked without Unity. **No Unity editor has opened these assets.** The hand
tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (the Ray's entry).

## 1. Translate the animal (step 1)

| question | the Ray's answer |
|---|---|
| what would a child draw? | a diamond, a whip, two lobes curling forward |
| how does it move? | a slow wave from shoulder to wingtip |
| the vessel of that name? | the Manta hull is a thin hard plane. So: woven, and it moves |
| with no up? | two wings (N = 2), and its two faces are identical, so it rolls freely. The whip and the lobes are on or about the axis |

Plan: wings woven between two edges. Spec: `Tools/Build/fauna_kit/specs/ray.py` (the top half).

## 2. The body (steps 2 to 4)

- **Limbs**: `Core` on the axis; per wing a `Lead` edge, a `Trail` edge and a `Lobe`; a `Whip` behind. Each is
  `K.grown(...)` with 4 or 5 points, built once inside `for k, a in enumerate(K.angles(N, phase=0.0))`.
- **Membranes**: `Web{k}` between `Lead{k}` and `Trail{k}`.
- **Wave**: the leading edge has `sway` (amp 7.5, `pow` 1.5, so the tip moves most); the hind edge the same with
  `phase` 0.9, so it runs a beat behind. Nothing is keyed.
- **Prisms**: four plates a wing, placed with `K.along(limb, t)`, outer ones first; one sting on the whip's end.
- **`"elements": K.elements(prisms, mass=1.25)`**: the leviathan is a quarter bigger than the base body.

```bash
python3 Tools/Build/fauna_kit/fauna.py check ray          # RESULT: PASS; base mesh 2,408 vertices
python3 Tools/Build/fauna_kit/fauna.py sheet ray <png>
```

## 3. The four element forms (step 6.5)

One function a form, in `specs/ray.py`: `comet`, `storm`, `leviathan`, `ghost`.

| element | form | feeling | what it adds | how |
|---|---|---|---|---|
| Time | the comet | joy | a streamer from each wingtip | `K.grown(..., design="whip")` parented to `Lead{k}`; nothing changed |
| Charge | the storm | awe with fear | thorns, a talon at each wingtip, a trident tail | `"change"` gives the edges `barbs` with `"dir": [0, 0, 1]` and `[0, 0, -1]`; `Talon{k}`; `Tine{k}` |
| Mass | the leviathan | reverence | four ribs across each wing, a tail fluke | ribs are `design="rib", step=2.0` with `to_limb`; the fluke is a membrane between two short limbs |
| Space | the ghost | longing | a torn sleeve from the outer half of each hind edge | four hanging `vein` limbs a wing, longest at the tip, a membrane between neighbours |

What went wrong on the way, and the check that said so:

| first attempt | what the check said | the fix |
|---|---|---|
| the Charge tines hung from the `Whip` | `L5 stinger 'Sting' rides 'Whip', which has child limbs` | hang them from the `Core`'s end, beside the whip |
| eight thick ribs on Mass | 5,277 vertices, over the 5,072 bar | `step=2.0` on the ribs: a straight strut needs few rings |
| the ghost's veil along the WHOLE hind edge | silhouette 0.35, nearest the Sparrow hull (bar 0.41) | hang it from the outer half only: 0.44 |
| barbs with no `"dir"` | the two wings' thorns pointed opposite ways on the sheet | state the side |

```bash
python3 Tools/Build/fauna_kit/elements.py --check ray          # 2,688 / 3,650 / 4,575 / 2,838
python3 Tools/Build/fauna_kit/elements.py --silhouette ray     # 0.49 / 0.45 / 0.45 / 0.44 against a bar of 0.41
python3 Tools/Build/fauna_kit/elements.py --sheet ray <png>
```

## 4. The lab (step 6)

`ray` is in `lab_export.NEW_SPECIES`. `python3 Tools/Build/fauna_kit/lab_export.py`, then `verify_lab.cjs`. In the
lab: pick Ray, then Time / Charge / Mass / Space. A person rides each and says which feelings land.

## 5. Into Unity (step A, B, C)

The spec's `"unity"` block names three moving parts, which is what makes the game's attack a WHIP (three limbs under
the body): `VaneA`, `VaneB` (each wing, cut at x = 3, 13, 24) and `Lash` (the whip, cut at z = -32, -42, -52). A form
says which part each limb it adds rides: `"unity": {"wires": {"VaneA": ["Rib00", ...]}}`.

```bash
python3 Tools/Build/fauna_kit/unity_port.py ray
python3 Tools/Build/fauna_kit/unity_port.py ray --check
```

It writes, for EACH element: a prefab (`Ray`, `RayCharge`, `RayMass`, `RaySpace`), ten meshes, a material; and once: a
data asset, four configs, four network entries. Then `author_spawn_matrix_roster.py` adds the Spawn Matrix row.

What each piece is for, so you can explain it:

| piece | what it does |
|---|---|
| the meshes' two extra channels | every vertex carries the sine part and the cosine part of its own wave (the same numbers the lab plays) |
| `SpindleGrownSway.hlsl` in SpindleGraph | adds that wave in the vertex shader from the platform clock: no joints, no per-frame CPU. It is off (`_GrownSway` 0) for every other mesh in the game |
| the form's material | SpindleMaterial with `_GrownSway` 1, the form's tempo, and the old shear sway off |
| `GrownLimbDriver` riders | each prism is carried along the wave of the limb it sits on, from the same clock, because a prism is a collider |
| the joints | only what the attack whips. They do not make the wave |
| `RayFauna` | feeds on the wing; glides under a rider (turn x 0.3, speed ease 0.9); rolls with its rider's frame |
| `spindleAuthoredEroded` on the Space prefab | the Space body is already thinned by the law; the game must not thin it again (it would tear the wing 8 u at its hinges: `erosion.py Ray`) |

What went wrong on the way:

| first attempt | how it was found | the fix |
|---|---|---|
| each jointed piece's mesh in its own space | the shared shear sway is a function of a vertex's z: computed for both sides of a cut it differed by 2.6 u | every piece keeps the body's space; its skin is set back by its joint's place |
| the wave only at the joints | the lab rippled and Unity would have folded at three hinges | the wave went into the shader |
| the game's Space erosion left on | `erosion.py` (the game's erosion ported, pinned to its C# tests) drew a crumpled wing | the Space form is authored eroded |
| Mass at x 0.58 | the kit took the SMALLEST body that clears the shields | Mass is never under the base body (law L8) |
| Mass unshielded, Charge not dangerous | running the game's spawn order in Amoebius: 0 of 8 shielded. The shipped QuadFish was the same | the game armed a body before it rolled its element; `Fauna.AssignLineage` arms it after (a fix for every light fauna) |

## 6. What is proven, and what only a person can judge

| proven here, without Unity | by |
|---|---|
| the laws, the mesh budgets, the outlines | `fauna.py check`, `elements.py --check`, `--silhouette` |
| the shader function compiles and is the kit's wave; off is exactly off | `Tools/Shaders/verify_spindle_grown_sway.py` (clang on the shipped file) |
| the five spindle graphs are wired right | `wire_spindle_grown_sway.py --check`, `wire_spindle_lod.py --check` |
| every mesh vertex carries the spec's position and wave; every prism is at its seat; the driver, class and material are on the prefab | `unity_port.py ray --check`, `--self-test` (five planted defects) |
| the C# compiles against Unity's reference assemblies | `Tools/Build/unity_refcompile/run.sh` (0 errors in changed files) |
| the game's own code runs on the prefabs: they load, spawn, swim; prisms ride the wave exactly; Mass is shielded, Charge all danger; Space is not thinned twice | a run in Amoebius (`/amoebius`): spawn each element's config through the spawner's own calls and read the prisms back. Do this for every creature you port; it found the armour fault no file check could |

| only a person, in the editor | where |
|---|---|
| that the assets import, draw and move as the lab does | `Docs/UNITY_VERIFICATION_CHECKLIST.md`, the Ray's entry |
| whether each feeling lands | the lab's Feedback tab, then the game |
