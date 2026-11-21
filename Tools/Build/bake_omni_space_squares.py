"""
Bakes the Space element's SQUARES animation on the omni crystal out of the artist's Blender file, for
the Omni Shepard Lab (Docs/Studios/OmniShepardLab.html, round 6).

The source is `CrystalsRaw_V2.blend` (kept by the art team, not in this repo), object
`OmniCrystal.001`: the shipped omni model carrying three shape keys on its 60 square plates,

    SquareRotateAroundPentagon   relative to Basis                 frames  1 -> 40, then snaps to 0
    SquareOverTri_Step1/2        relative to Basis                 frames 41 -> 80, then held
    SquareOverTri_Step2/2        relative to SquareOverTri_Step1/2  frames 81 -> 120 (stacked)

played by Action.008 at 25 fps: a 4.8 s loop in which the squares turn about the pentagons and then
weave over the triangles. Shape keys are exact data, so this writes them as they are: the Basis,
each key's delta against ITS relative key, and every frame's three key values (the Bezier curves,
evaluated by Blender). The lab replays pos = basis + sum(w_k * delta_k).

Run (Blender 4.5, headless):

    blender -b CrystalsRaw_V2.blend --python Tools/Build/bake_omni_space_squares.py -- Docs/Studios/data/omni_space_squares.json

It refuses to write unless the replay matches Blender's own evaluated shape-key mesh (modifiers off)
at every frame to 1e-5. Tools/Build/omni_shepard_lab_assets.py then maps the Basis onto the shipped
body FBX (OmniCrystalExport1_8-21-25.fbx) vertex for vertex and bakes the result into the lab.
"""

import json
import sys

import bpy

OBJECT = "OmniCrystal.001"
out_path = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "omni_space_squares.json"

ob = bpy.data.objects[OBJECT]
me = ob.data
keys = me.shape_keys
kb = keys.key_blocks
if not keys.use_relative:
    raise SystemExit(f"{OBJECT}: shape keys are absolute; this baker reads relative keys only")

basis = [tuple(v.co) for v in kb["Basis"].data]
moving = [k for k in kb if k.name != "Basis"]
out_keys = []
for k in moving:
    rel = k.relative_key
    idx, delta = [], []
    for i, (a, b) in enumerate(zip(k.data, rel.data)):
        d = (a.co[0] - b.co[0], a.co[1] - b.co[1], a.co[2] - b.co[2])
        if max(abs(c) for c in d) > 1e-7:
            idx.append(i)
            delta += [round(c, 6) for c in d]
    out_keys.append({"name": k.name, "relative": rel.name, "verts": idx, "delta": delta})

scene = bpy.context.scene
frames = list(range(int(scene.frame_start), int(scene.frame_end) + 1))
weights = []
for f in frames:
    scene.frame_set(f)
    weights.append([round(k.value, 6) for k in moving])

# Proof: replay vs Blender's evaluated mesh (shape keys only; the UnityShader1 node group only splits
# edges and extrudes for display, so it is switched off for the comparison).
for m in ob.modifiers:
    m.show_viewport = False
worst = 0.0
for fi, f in enumerate(frames):
    scene.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    mesh = ev.to_mesh()
    rep = [list(p) for p in basis]
    for k, w in zip(out_keys, weights[fi]):
        for j, v in enumerate(k["verts"]):
            for c in range(3):
                rep[v][c] += w * k["delta"][3 * j + c]
    for i, v in enumerate(mesh.vertices):
        worst = max(worst, max(abs(v.co[c] - rep[i][c]) for c in range(3)))
    ev.to_mesh_clear()
print(f"bake_omni_space_squares: replay vs Blender over {len(frames)} frames: max |diff| {worst:.2e}")
if worst > 1e-5:
    raise SystemExit("bake_omni_space_squares: the replay does not match Blender; nothing written")

data = {
    "source": "CrystalsRaw_V2.blend / " + OBJECT + " (Action.008)",
    "fps": scene.render.fps, "frameStart": frames[0], "frameEnd": frames[-1],
    "basis": [round(c, 6) for p in basis for c in p],
    "keys": out_keys,
    "weights": weights,
}
with open(out_path, "w") as fh:
    json.dump(data, fh, separators=(",", ":"))
print(f"bake_omni_space_squares: wrote {out_path}: {len(basis)} verts, {len(out_keys)} keys, {len(frames)} frames")
