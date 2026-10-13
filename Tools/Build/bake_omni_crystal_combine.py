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
