"""
Bakes the artist's OMNI COMBINE targets out of `CrystalsRaw.blend`, for the Omni Combine Lab
(Docs/Studios/OmniCombineLab.html, Docs/Studios/OMNI_COMBINE_LAB.md).

The .blend (kept by the art team, not in this repo) lays the four elemental crystals out in a row beside
the omni crystal. Each carries ONE shape key, `Key 1`, that moves its plates onto its share of the omni,
in the crystal's own space at the omni's size:

    Time    1.011      30 rhombi            -> the omni's 30 rhombi     (one to one)
    Space   Solid.060  60 squares           -> the omni's 60 squares    (one to one)
    Mass    1.014      60 triangular prisms -> the omni's 20 triangles  (3 stacked per triangle)
    Charge  1.012      60 pentagon plates   -> the omni's 12 pentagons  (5 stacked per pentagon)

Together they cover all 960 of the omni's (`1.013`) vertices. Nothing in the file ANIMATES `Key 1` (no
action, driver or NLA strip; its value is 0): the combine's END POSE is authored, the motion is not. The
lab invents the motion; this file carries only what the artist made.

Shape keys are exact data, so this writes them as they are: per crystal its Basis, its Key 1, its
triangles, each vertex's plate, the omni vertex each Key 1 vertex lands on, and its object offset from
the omni. It refuses to write unless every Key 1 vertex lands on an omni vertex to 1e-3 (each crystal's worst is recorded) and the four
together cover every omni vertex. Coordinates are Blender's (Z up); the lab converts.

Run (Blender 4.5, headless):

    blender -b CrystalsRaw.blend --python Tools/Build/bake_omni_crystal_combine.py -- Docs/Studios/data/omni_crystal_combine.json
"""

import json
import sys

import bpy
from mathutils import kdtree

OMNI = "1.013"
CRYSTALS = [("Charge", "1.012"), ("Mass", "1.014"), ("Space", "Solid.060"), ("Time", "1.011")]
KEY = "Key 1"
LAND = 1e-3   # Time lands 2.9e-4 off (the artist's snap), 0.02% of the omni's radius; the rest ~1e-5

out_path = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "omni_crystal_combine.json"
r5 = lambda v: round(v, 5)


def flat(vectors):
    return [r5(c) for v in vectors for c in v]


def triangles(mesh):
    mesh.calc_loop_triangles()
    return [i for t in mesh.loop_triangles for i in t.vertices]


def plates(mesh):
    """Each vertex's plate: a connected component of the mesh's edges, numbered by first vertex."""
    parent = list(range(len(mesh.vertices)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for e in mesh.edges:
        parent[find(e.vertices[0])] = find(e.vertices[1])
    ids, out = {}, []
    for i in range(len(mesh.vertices)):
        out.append(ids.setdefault(find(i), len(ids)))
    return out


omni = bpy.data.objects[OMNI]
if omni.data.shape_keys and len(omni.data.shape_keys.key_blocks) > 1:
    raise SystemExit(f"{OMNI}: the omni grew shape keys - this baker reads its rest mesh only")
omni_co = [v.co.copy() for v in omni.data.vertices]
tree = kdtree.KDTree(len(omni_co))
for i, v in enumerate(omni_co):
    tree.insert(v, i)
tree.balance()

covered = set()
crystals = []
for element, name in CRYSTALS:
    ob = bpy.data.objects[name]
    keys = ob.data.shape_keys
    if not keys or KEY not in keys.key_blocks:
        raise SystemExit(f"{element} ({name}): no '{KEY}' shape key")
    if keys.key_blocks[KEY].relative_key.name != "Basis":
        raise SystemExit(f"{element} ({name}): '{KEY}' is not relative to the Basis")
    if any(abs(c) > 1e-6 for c in ob.rotation_euler) or any(abs(s - 1) > 1e-6 for s in ob.scale):
        raise SystemExit(f"{element} ({name}): rotated or scaled - Key 1 would not land in the omni's frame")
    if ob.parent or omni.parent:
        raise SystemExit(f"{element} ({name}) or the omni is parented - offsets would be wrong")
    basis = [d.co.copy() for d in keys.key_blocks["Basis"].data]
    key1 = [d.co.copy() for d in keys.key_blocks[KEY].data]
    lands, worst = [], 0.0
    for v in key1:
        _, j, dist = tree.find(v)
        lands.append(j)
        worst = max(worst, dist)
    if worst > LAND:
        raise SystemExit(f"{element} ({name}): Key 1 lands {worst:.2e} from the omni (> {LAND})")
    covered.update(lands)
    stack = len(lands) // len(set(lands))
    crystals.append({
        "element": element, "object": name,
        "offset": [r5(a - b) for a, b in zip(ob.location, omni.location)],
        "basis": flat(basis), "key1": flat(key1), "tris": triangles(ob.data), "plate": plates(ob.data),
        "lands": lands, "stack": stack, "landErr": float(f"{worst:.2e}"),
    })
    print(f"{element:6s} {name:10s} {len(basis)} verts, {max(crystals[-1]['plate']) + 1} plates, "
          f"lands on {len(set(lands))} omni verts ({stack} per), worst {worst:.1e}")

# The Space crystal's own animation (its idle state before it joins the combine): Solid.055 is the Space
# crystal (Solid.060) vertex for vertex at another scale, carrying four shape keys - the squares spinning
# about the 5-point vertices in two half spins, then about the 3-point vertices - played by Solid.081Action
# over 120 frames. Baked as deltas in Solid.060's vertex order and frame, plus every frame's weights, and
# refused unless the replay matches Blender's evaluated mesh at sampled frames.
SPACE_ANIM = "Solid.055"
space = next(c for c in crystals if c["element"] == "Space")
sp_obj = bpy.data.objects[space["object"]]
src = bpy.data.objects[SPACE_ANIM]
s_basis = [d.co.copy() for d in sp_obj.data.shape_keys.key_blocks["Basis"].data]
a_basis = [d.co.copy() for d in src.data.shape_keys.key_blocks["Basis"].data]
scale = max(v.length for v in s_basis) / max(v.length for v in a_basis)
atree = kdtree.KDTree(len(a_basis))
for i, v in enumerate(a_basis):
    atree.insert(v * scale, i)
atree.balance()
amap, aworst = [], 0.0
for v in s_basis:
    _, j, dist = atree.find(v)
    amap.append(j)
    aworst = max(aworst, dist)
if aworst > 1e-4 or len(set(amap)) != len(amap):
    raise SystemExit(f"{SPACE_ANIM} is not the Space crystal at another scale (worst {aworst:.2e})")
kbs = src.data.shape_keys.key_blocks
names = [k.name for k in kbs if k.name != "Basis"]
akeys = []
for k in kbs:
    if k.name == "Basis":
        continue
    rel = k.relative_key
    delta = [(k.data[j].co - rel.data[j].co) * scale for j in amap]
    akeys.append({"name": k.name, "relative": rel.name, "delta": [r5(c) for v in delta for c in v]})
action = src.data.shape_keys.animation_data.action
f0, f1 = int(action.frame_range[0]), int(action.frame_range[1])
curves = {fc.data_path: fc for fc in action.fcurves}
aweights = []
for f in range(f0, f1 + 1):
    aweights.append([round(curves[f'key_blocks["{n}"].value'].evaluate(f), 6) if f'key_blocks["{n}"].value' in curves else 0.0 for n in names])
# Replay check against Blender's own evaluation at sampled frames.
scene = bpy.context.scene
for f in (f0, (f0 + f1) // 4, (f0 + f1) // 2, 3 * (f0 + f1) // 4, f1):
    scene.frame_set(f)
    ev = src.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    w = aweights[f - f0]
    worst = 0.0
    for n_i, j in enumerate(amap):
        replay = s_basis[n_i].copy()
        for ki, k in enumerate(akeys):
            replay += w[ki] * __import__("mathutils").Vector(k["delta"][3 * n_i:3 * n_i + 3])
        worst = max(worst, (replay - ev.vertices[j].co * scale).length)
    src.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    if worst > 1e-4:
        raise SystemExit(f"{SPACE_ANIM} replay at frame {f} is {worst:.2e} off Blender's own mesh")
space["idle"] = {"source": f"{SPACE_ANIM} ({action.name}), x{scale:.5f}", "fps": scene.render.fps,
                 "keys": akeys, "weights": aweights}
print(f"Space idle: {len(akeys)} keys, frames {f0}-{f1}, replay matches Blender")

if len(covered) != len(omni_co):
    raise SystemExit(f"the four crystals land on {len(covered)} of the omni's {len(omni_co)} vertices")

data = {
    "source": "CrystalsRaw.blend: Time 1.011, Space Solid.060, Mass 1.014, Charge 1.012 (shape key 'Key 1') onto omni 1.013",
    "axes": "Blender (Z up)",
    "omni": {"verts": flat(omni_co), "tris": triangles(omni.data), "plate": plates(omni.data)},
    "crystals": crystals,
}
with open(out_path, "w") as f:
    json.dump(data, f, separators=(",", ":"))
print(f"wrote {out_path}: all {len(omni_co)} omni vertices covered")
