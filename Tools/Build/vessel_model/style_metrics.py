#!/usr/bin/env python3
"""
style_metrics - measure a vessel model's STYLE the way the fleet's accepted artist hulls carry it,
so a hull built from nothing (the Butterfly, 2026-10-18) is held to the fleet's look by numbers
as well as by the owner's eye.

The from-scratch rebuilds of 2026-10-17 read as "cheap remakes" next to the artist hulls; the
difference is measurable:
  - SURFACE: the artist hulls are smooth-shaded sculpted forms (most edges meet at shallow angles,
    hard edges are rare and deliberate); the primitives hulls were faceted lathes and plates.
  - DOMAIN as LINE WORK: the domain colour is laid as many thin strips, rims and decals (the
    Dolphin's trim lines, the Squirrel's panel edges), not a few big painted blocks.
  - DETAIL DENSITY: polygons per unit of surface, and how many separate painted regions a hull
    carries per unit of its length.
  - GLASS and ENGINE: present, readable shares of the surface.

    python3 Tools/Build/vessel_model/style_metrics.py <file.fbx> [...]          one row per file
    python3 Tools/Build/vessel_model/style_metrics.py --fleet                   the seven artist files (pinned ref)
    python3 Tools/Build/vessel_model/style_metrics.py --gate <file.fbx>         compare with the fleet band, exit 1 outside

Slots are read by POSITION in the fleet convention (the artist files name them differently):
the spec passes `slot_roles` = (domain, body, window, engine) indices when the order differs.
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

_MEASURE = r'''
import bpy, bmesh, sys, json, math
path, roles = sys.argv[-2], json.loads(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=False)
mesh = [o for o in bpy.data.objects if o.type == "MESH"][0]
if mesh.data.shape_keys:
    for k in mesh.data.shape_keys.key_blocks:
        k.value = 0.0
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
me = mesh.evaluated_get(dg).to_mesh()
bm = bmesh.new(); bm.from_mesh(me)
bm.transform(mesh.matrix_world)
bm.normal_update()
names = [m.name if m else "" for m in mesh.data.materials]
def role_of(idx):
    for r, i in roles.items():
        if i == idx:
            return r
    return "other"
area = {}
tris = 0
for f in bm.faces:
    r = role_of(f.material_index)
    area[r] = area.get(r, 0.0) + f.calc_area()
    tris += len(f.verts) - 2
total = sum(area.values()) or 1.0
xs = [v.co for v in bm.verts]
lo = [min(p[i] for p in xs) for i in range(3)]; hi = [max(p[i] for p in xs) for i in range(3)]
ext = sorted([hi[i] - lo[i] for i in range(3)], reverse=True)
diag = math.sqrt(sum(e * e for e in ext))
# smoothness: share of interior edges (by length) whose faces meet within 20 / beyond 45 degrees
smooth_l = hard_l = all_l = 0.0
for e in bm.edges:
    if len(e.link_faces) != 2:
        continue
    L = e.calc_length(); all_l += L
    a = math.degrees(e.calc_face_angle(0.0))
    if a < 20: smooth_l += L
    elif a > 45: hard_l += L
# painted regions: connected components of same-role faces (across shared edges)
seen = set(); regions = {}
for f in bm.faces:
    if f.index in seen:
        continue
    r = role_of(f.material_index); stack = [f]; seen.add(f.index); a_ = 0.0; perim = 0.0
    while stack:
        g = stack.pop(); a_ += g.calc_area()
        for e in g.edges:
            other = [h for h in e.link_faces if h is not g]
            if not other or role_of(other[0].material_index) != r:
                perim += e.calc_length()
            for h in other:
                if h.index not in seen and role_of(h.material_index) == r:
                    seen.add(h.index); stack.append(h)
    regions.setdefault(r, []).append((a_, perim))
def region_stats(r):
    rs = [x for x in regions.get(r, []) if x[0] > 1e-9]
    if not rs:
        return {"count": 0, "width_rel": 0.0}
    # characteristic width of a painted region = 2*area/perimeter (a strip's width), area-weighted, over the diagonal
    tot = sum(a for a, p in rs)
    w = sum(a * (2 * a / p if p > 0 else 0) for a, p in rs) / tot
    return {"count": len(rs), "width_rel": w / diag}
shells = 0; seenv = set()
for v in bm.verts:
    if v.index in seenv: continue
    shells += 1; stack = [v]; seenv.add(v.index)
    while stack:
        u = stack.pop()
        for e in u.link_edges:
            o = e.other_vert(u)
            if o.index not in seenv:
                seenv.add(o.index); stack.append(o)
out = {"file": path, "tris": tris, "verts": len(bm.verts), "diag": diag, "extent": ext,
       "share": {r: area.get(r, 0.0) / total for r in ("domain", "body", "window", "engine")},
       "smooth_edge_share": smooth_l / (all_l or 1), "hard_edge_share": hard_l / (all_l or 1),
       "tris_per_area": tris / total * diag * diag / 1000.0,
       "domain_regions": region_stats("domain"), "window_regions": region_stats("window"), "engine_regions": region_stats("engine"),
       "shells": shells, "slots": names}
print("STYLE_JSON " + json.dumps(out))
'''

# the artist files' slot roles (the fleet convention reads slot 0 as domain; the artist files predate it)
FLEET = {
    "squirrel": ("Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx", {"domain": 0, "body": 1, "window": 2, "engine": 3}),
    "dolphin": ("Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx", {"domain": 0, "body": 1, "window": 2, "engine": 3}),
    "sparrow": ("Assets/_Models/Vessel Models/SparrowModel1.fbx", {"body": 0, "domain": 1, "engine": 2, "window": 3}),
    "serpent": ("Assets/_Models/Vessel Models/SerpentExport.fbx", {"body": 0, "window": 1, "domain": 2, "engine": 3}),
    "manta": ("Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx", {"domain": 0, "body": 1, "window": 2, "engine": 3}),
}
ARTIST_REF = "b7f0fbbf3"


def measure(path, roles):
    p = subprocess.run([sys.executable, "-c", _MEASURE, path, json.dumps(roles)], capture_output=True, text=True, timeout=900)
    for line in p.stdout.splitlines():
        if line.startswith("STYLE_JSON "):
            return json.loads(line[len("STYLE_JSON "):])
    raise RuntimeError("style measure failed on %s:\n%s" % (path, (p.stdout + p.stderr)[-1500:]))


def fleet():
    sys.path.insert(0, HERE)
    import build as B
    out = {}
    for name, (rel, roles) in FLEET.items():
        out[name] = measure(B.artist_path(rel), roles)
    return out


def band(rows):
    """The fleet band per metric: (min, max) over the accepted artist hulls."""
    keys = {
        "smooth_edge_share": lambda m: m["smooth_edge_share"],
        "hard_edge_share": lambda m: m["hard_edge_share"],
        "domain_share": lambda m: m["share"]["domain"],
        "window_share": lambda m: m["share"]["window"],
        "engine_share": lambda m: m["share"]["engine"],
        "domain_regions": lambda m: m["domain_regions"]["count"],
        "domain_width_rel": lambda m: m["domain_regions"]["width_rel"],
        "tris_per_area": lambda m: m["tris_per_area"],
    }
    return {k: (min(f(m) for m in rows), max(f(m) for m in rows)) for k, f in keys.items()}, keys


def gate(m, fl, slack=0.25):
    """Failures for a candidate outside the fleet band (widened by `slack` of the band's range,
    and never tighter than the fleet's own spread)."""
    b, keys = band(list(fl.values()))
    fails = []
    for k, (lo, hi) in b.items():
        v = keys[k](m)
        pad = slack * max(hi - lo, abs(hi) * 0.1, 1e-6)
        if v < lo - pad or v > hi + pad:
            fails.append("%s %.4g is outside the fleet's %.4g .. %.4g" % (k, v, lo, hi))
    return fails


def _row(name, m):
    return "%-10s tris %6d  smooth %.2f hard %.2f  dom %.2f win %.3f eng %.3f  dom-regions %4d width %.4f  tris/area %.2f  shells %d" % (
        name, m["tris"], m["smooth_edge_share"], m["hard_edge_share"], m["share"]["domain"], m["share"]["window"],
        m["share"]["engine"], m["domain_regions"]["count"], m["domain_regions"]["width_rel"], m["tris_per_area"], m["shells"])


def main():
    import argparse
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*")
    ap.add_argument("--fleet", action="store_true")
    ap.add_argument("--gate", action="store_true")
    ap.add_argument("--roles", default='{"domain": 0, "body": 1, "window": 2, "engine": 3}')
    a = ap.parse_args()
    fl = None
    if a.fleet or a.gate:
        fl = fleet()
        for n, m in fl.items():
            print(_row(n, m))
        b, _k = band(list(fl.values()))
        print("fleet band: " + ", ".join("%s %.3g..%.3g" % (k, lo, hi) for k, (lo, hi) in b.items()))
    rc = 0
    for f in a.files:
        m = measure(f, json.loads(a.roles))
        print(_row(os.path.basename(f)[:10], m))
        if a.gate:
            fails = gate(m, fl)
            for x in fails:
                print("  OUTSIDE THE FLEET: " + x)
            rc = rc or (1 if fails else 0)
    sys.exit(rc)


if __name__ == "__main__":
    main()
