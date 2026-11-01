#!/usr/bin/env python3
"""Build THE BEE's model (Garrett, 2026-11-01: "make a real bee model with /fauna"; Docs/ECOSYSTEM.md §66).

    <python with bpy> Tools/Build/bee_model/build_bee.py                 # write Assets/_Models/Fauna/Bee.fbx
    <python with bpy> Tools/Build/bee_model/build_bee.py --out X.fbx     # write somewhere else (a check build)
    <python with bpy> Tools/Build/bee_model/build_bee.py --render DIR    # + a contact sheet of the exported file

Blender-as-a-module (the /vessel-model pipeline's install: on a python3.13 box, `python3.11 -m venv <scratch>/bpyenv &&
<scratch>/bpyenv/bin/pip install bpy numpy pillow`). Writes the FBX only; Tools/Build/author_bee_assets.py owns its
.meta (the material remap) and the prefab that nests it.

THE FRAME: the prefab's LOCAL units (the bee's root carries the uniform body scale, author_bee_assets.BEE_BODY_SCALE), so a
model vertex at (x, y, z) in Blender lands at Unity local (x, z, y): Blender +Y is the bee's forward (Unity +z), Blender
+Z its up (Unity +y). Written with the vessel pipeline's `zup_identity` axes (UnitScaleFactor 100: lands 1:1).

THE ANATOMY, and the fauna contract it keeps (the /fauna skill):
  * the HEAD is the heart: the lifeform's crystal sits at the origin, the front of the body with the body trailing (§23.9,
    the tadpole arrangement), so the model has no head shell to bury it in - two compound-eye domes flank it and the
    antennae rise from just behind it and reach forward past it;
  * THORAX (fuzzy: displaced, flat-shaded facets), a waist, and an ABDOMEN of four fuzzy bands parted by three recessed
    grooves (the stripes, in form: one material slot, because the body is the Spindle's renderer and the spindle LOD owns
    its material) ending in a stinger; six jointed legs, the hind pair with pollen baskets;
  * FOUR WINGS, each its own object pivoted at its root (Bee_WingFL / FR / HL / HR), so BeeWingDriver flaps them by name
    (localRotation only); the membrane is its own slot (BeeWing);
  * NO body prism is in the model: the bee's conserved mass stays its HealthPrisms (the abdomen core and the two forewing
    veins), posed by the generator to sit inside the abdomen and along the forewings' leading edges.
"""
import argparse
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))   # Tools/Build/bee_model -> the repo
assert os.path.isdir(os.path.join(REPO, "Assets")), f"REPO {REPO} has no Assets/ (a ROOT one dirname short writes the tree elsewhere)"
OUTPUT = os.path.join(REPO, "Assets", "_Models", "Fauna", "Bee.fbx")
sys.path.insert(0, os.path.dirname(HERE))   # Tools/Build, for vessel_model

# ── every number of the model (prefab-local units; the heart's runtime half-extent is about 1.16 here) ──────────────
THORAX = dict(centre=(0.0, -2.35, 0.15), radii=(1.42, 1.45, 1.36), fuzz=0.2)
WAIST = dict(centre=(0.0, -3.7, 0.05), radius=0.42)
ABDOMEN = dict(start=-3.95, end=-8.2, max_radius=1.95, bulge_at=0.42, bands=4, groove_depth=0.34, fuzz=0.17)
STINGER = dict(length=0.4, radius=0.16)
EYES = dict(centre=(1.2, -0.45, 0.25), radii=(0.5, 0.8, 0.72))
ANTENNA = dict(root=(0.32, -0.95, 0.95), elbow=(0.55, -0.55, 2.05), tip=(0.95, 1.45, 2.45), radius=0.075)
LEGS = [  # (root y, femur end, tibia end, tarsus end) for the +x side; mirrored
    (-1.75, (1.85, -1.35, -1.25), (2.25, -1.55, -2.35), (2.15, -1.25, -2.85)),
    (-2.35, (2.0, -2.35, -1.3), (2.4, -2.75, -2.45), (2.3, -3.05, -2.95)),
    (-2.95, (1.95, -3.55, -1.25), (2.35, -4.55, -2.3), (2.2, -5.35, -2.75)),
]
POLLEN_BASKET = dict(radius=0.34)          # the hind tibia's corbicula
FOREWING = dict(pivot=(1.4, -1.2, 1.1), span=4.6, chord=1.7, sweep=1.15, dihedral_deg=9)
HINDWING = dict(pivot=(1.25, -1.9, 0.95), span=3.0, chord=1.05, sweep=0.85, dihedral_deg=9)
WING_THICKNESS = 0.05
SEED = 1301


def _clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _new(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _material(name, rgba):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = rgba
    return m


def _fuzz(bm, amount, scale=2.6, centre=Vector((0, 0, 0))):
    """Displace every vertex along its normal by a seeded noise: a short pile that flat shading turns into facets."""
    bm.normal_update()
    for v in bm.verts:
        n = noise.noise((v.co + Vector((SEED, 0, 0))) * scale)
        v.co += v.normal * amount * n


def ellipsoid(radii, centre, subdiv=3, fuzz=0.0):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    if fuzz:
        _fuzz(bm, fuzz)
    bmesh.ops.translate(bm, vec=Vector(centre), verts=bm.verts)
    return bm


def abdomen_profile(t):
    """The abdomen's radius at t in [0, 1] (front to back): a teardrop with banded fuzz and recessed grooves (the
    stripes): a groove between each pair of bands."""
    a = ABDOMEN
    base = a["max_radius"] * math.sin(math.pi * min(1.0, t / (2 * a["bulge_at"]))) if t < a["bulge_at"] else \
        a["max_radius"] * math.sqrt(max(0.0, 1.0 - ((t - a["bulge_at"]) / (1 - a["bulge_at"])) ** 2.2 * 0.93))
    base = max(base, 0.18)
    phase = t * a["bands"]
    frac = phase - math.floor(phase)
    groove = 0.0
    if 0.2 < t < 0.97 and frac < 0.22:                        # a groove at the front of each band past the first
        groove = a["groove_depth"] * math.sin(math.pi * frac / 0.22)
    return base - groove


def lathe_abdomen():
    a = ABDOMEN
    rings, seg = 44, 20
    bm = bmesh.new()
    loops = []
    for i in range(rings + 1):
        t = i / rings
        y = a["start"] + (a["end"] - a["start"]) * t
        r = abdomen_profile(t)
        loop = []
        for k in range(seg):
            ang = 2 * math.pi * k / seg
            loop.append(bm.verts.new((r * math.cos(ang), y, 0.05 + r * math.sin(ang) * 0.95)))
        loops.append(loop)
    for i in range(rings):
        for k in range(seg):
            bm.faces.new((loops[i][k], loops[i][(k + 1) % seg], loops[i + 1][(k + 1) % seg], loops[i + 1][k]))
    cap_f = bm.verts.new((0, a["start"] + 0.08, 0.05))
    for k in range(seg):
        bm.faces.new((cap_f, loops[0][(k + 1) % seg], loops[0][k]))
    # the stinger: a cone off the last ring
    tip = bm.verts.new((0, a["end"] - STINGER["length"], 0.05))
    for k in range(seg):
        bm.faces.new((loops[-1][k], loops[-1][(k + 1) % seg], tip))
    bm.normal_update()
    # fuzz on the bands only (the grooves stay smooth: that contrast is the stripe)
    for v in bm.verts:
        t = (v.co.y - a["start"]) / (a["end"] - a["start"])
        if not (0.0 <= t <= 1.0):
            continue
        frac = (t * a["bands"]) % 1.0
        if frac < 0.24:
            continue
        n = noise.noise((v.co + Vector((SEED, 7, 0))) * 3.1)
        v.co += v.normal * a["fuzz"] * n
    return bm


def tube(points, radius, seg=6, end_radius=None):
    """A tube through `points` (a jointed leg or an antenna), radius tapering to `end_radius`."""
    end_radius = radius if end_radius is None else end_radius
    bm = bmesh.new()
    pts = [Vector(p) for p in points]
    loops = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 1e-3:
            side = d.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(d).normalized()
        r = radius + (end_radius - radius) * i / max(1, len(pts) - 1)
        loops.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / seg) + up * math.sin(2 * math.pi * k / seg)) * r)
                      for k in range(seg)])
    for i in range(len(loops) - 1):
        for k in range(seg):
            bm.faces.new((loops[i][k], loops[i][(k + 1) % seg], loops[i + 1][(k + 1) % seg], loops[i + 1][k]))
    a, b = bm.verts.new(pts[0]), bm.verts.new(pts[-1])
    for k in range(seg):
        bm.faces.new((a, loops[0][(k + 1) % seg], loops[0][k]))
        bm.faces.new((loops[-1][k], loops[-1][(k + 1) % seg], b))
    return bm


def mirror_x(bm):
    out = bm.copy()
    bmesh.ops.scale(out, vec=Vector((-1, 1, 1)), verts=out.verts)
    bmesh.ops.reverse_faces(out, faces=out.faces)
    return out


def merge(dst, src):
    me = bpy.data.meshes.new("_tmp")
    src.to_mesh(me)
    src.free()
    dst.from_mesh(me)
    bpy.data.meshes.remove(me)


def build_body(mat):
    bm = bmesh.new()
    merge(bm, ellipsoid(THORAX["radii"], THORAX["centre"], subdiv=3, fuzz=THORAX["fuzz"]))
    merge(bm, ellipsoid((WAIST["radius"],) * 3, WAIST["centre"], subdiv=2))
    merge(bm, lathe_abdomen())
    for side in (1, -1):
        ex, ey, ez = EYES["centre"]
        eye = ellipsoid(EYES["radii"], (side * ex, ey, ez), subdiv=2)
        merge(bm, eye)
        rx, ry, rz = ANTENNA["root"]
        ax, ay, az = ANTENNA["elbow"]
        tx, ty, tz = ANTENNA["tip"]
        merge(bm, tube([(side * rx, ry, rz), (side * ax, ay, az)], ANTENNA["radius"], seg=5))
        merge(bm, tube([(side * ax, ay, az), (side * (ax + tx) / 2, (ay + ty) / 2, (az + tz) / 2 + 0.25),
                        (side * tx, ty, tz)], ANTENNA["radius"], seg=5, end_radius=ANTENNA["radius"] * 1.6))
        for i, (ry_, femur, tibia, tarsus) in enumerate(LEGS):
            root = (side * 0.7, ry_, -0.75)
            f = (side * femur[0], femur[1], femur[2])
            t = (side * tibia[0], tibia[1], tibia[2])
            s = (side * tarsus[0], tarsus[1], tarsus[2])
            merge(bm, tube([root, f], 0.13, seg=5, end_radius=0.1))
            merge(bm, tube([f, t], 0.1, seg=5, end_radius=0.08 if i < 2 else 0.12))
            merge(bm, tube([t, s], 0.07, seg=5, end_radius=0.05))
            if i == 2:   # the pollen basket: a swelling on the hind tibia
                mid = ((f[0] + t[0]) / 2, (f[1] + t[1]) / 2, (f[2] + t[2]) / 2)
                merge(bm, ellipsoid((POLLEN_BASKET["radius"], POLLEN_BASKET["radius"] * 1.6, POLLEN_BASKET["radius"]),
                                    mid, subdiv=1))
    ob = _new("Bee_Body", bm)
    ob.data.materials.append(mat)
    return ob


def wing_outline(span, chord, sweep, n=18):
    """A bee wing's outline in its own frame: the root at the origin, the span along +x, the leading edge on +y; a
    rounded teardrop swept back at the tip."""
    pts = []
    for i in range(n + 1):           # the leading edge, root to tip
        s = i / n
        pts.append((span * s, 0.12 * chord * math.sin(math.pi * s) - sweep * s ** 2 * 0.35))
    for i in range(n, -1, -1):       # the trailing edge, tip to root
        s = i / n
        depth = chord * (0.35 + 0.65 * math.sin(math.pi * min(1.0, s * 1.15)))
        pts.append((span * s, -depth - sweep * s ** 2 * 0.35))
    return pts


def build_wing(name, w, side, mat):
    bm = bmesh.new()
    outline = wing_outline(w["span"], w["chord"], w["sweep"])
    top = [bm.verts.new((side * x, y, WING_THICKNESS / 2)) for x, y in outline]
    bot = [bm.verts.new((side * x, y, -WING_THICKNESS / 2)) for x, y in outline]
    f_top = bm.faces.new(top if side > 0 else list(reversed(top)))
    f_bot = bm.faces.new(list(reversed(bot)) if side > 0 else bot)
    n = len(outline)
    for k in range(n):
        q = (top[k], bot[k], bot[(k + 1) % n], top[(k + 1) % n])
        bm.faces.new(q if side > 0 else tuple(reversed(q)))
    bmesh.ops.triangulate(bm, faces=[f_top, f_bot])
    # the dihedral: each wing tilts up about the body's long axis (Blender Y), the root fixed
    ang = math.radians(w["dihedral_deg"]) * side
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(-ang, 3, "Y"), verts=bm.verts)
    ob = _new(name, bm)
    ob.data.materials.append(mat)
    px, py, pz = w["pivot"]
    ob.location = (side * px, py, pz)
    return ob


def build():
    _clear()
    body_mat = _material("BeeBody", (0.95, 0.72, 0.18, 1.0))
    wing_mat = _material("BeeWing", (0.8, 0.9, 1.0, 0.35))
    objects = [build_body(body_mat)]
    for side, tag in ((1, "R"), (-1, "L")):
        objects.append(build_wing(f"Bee_WingF{tag}", FOREWING, side, wing_mat))
        objects.append(build_wing(f"Bee_WingH{tag}", HINDWING, side, wing_mat))
    for ob in objects:
        for p in ob.data.polygons:
            p.use_smooth = False
    return objects


def tri_count(objects):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objects)


def export(objects, path):
    import vessel_model.export as X
    X.export_fbx(path, objects, "zup_identity", bake_anim=False)
    return path


def render(fbx_path, out_dir):
    """A contact sheet of the EXPORTED file re-imported, with the runtime extras the game adds drawn as stand-ins: the
    heart (an octahedron at the origin, the one crystal size), the abdomen core prism and the two forewing veins (boxes
    at the generator's poses). Every stand-in is drawn as itself and named in the sheet's caption."""
    import vessel_model.render as R
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    clay = {"BeeBody": (0.86, 0.62, 0.16, 1), "BeeWing": (0.75, 0.88, 1.0, 1)}
    for m in meshes:
        for i, mat in enumerate(m.data.materials):
            key = next((k for k in clay if mat and mat.name.startswith(k)), "BeeBody")
            mm = bpy.data.materials.new(f"clay_{key}")
            mm.use_nodes = True
            bsdf = mm.node_tree.nodes["Principled BSDF"]
            bsdf.inputs["Base Color"].default_value = clay[key]
            if key == "BeeWing":
                bsdf.inputs["Alpha"].default_value = 0.45
                bsdf.inputs["Roughness"].default_value = 0.15
            m.data.materials[i] = mm
    extras = []
    heart = bmesh.new()
    bmesh.ops.create_cone(heart, cap_ends=True, segments=4, radius1=1.16, radius2=0.0, depth=1.16)
    lower = bmesh.new()
    bmesh.ops.create_cone(lower, cap_ends=True, segments=4, radius1=1.16, radius2=0.0, depth=1.16)
    bmesh.ops.rotate(lower, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.pi, 3, "X"), verts=lower.verts)
    bmesh.ops.translate(heart, vec=Vector((0, 0, 0.58)), verts=heart.verts)
    bmesh.ops.translate(lower, vec=Vector((0, 0, -0.58)), verts=lower.verts)
    merge(heart, lower)
    h = _new("stand-in heart", heart)
    hm = bpy.data.materials.new("heart")
    hm.use_nodes = True
    hm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.1, 0.85, 0.95, 1)
    hm.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (0.1, 0.7, 0.8, 1)
    hm.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.6
    h.data.materials.append(hm)
    extras.append(h)
    meshes.extend(extras)
    files = []
    os.makedirs(out_dir, exist_ok=True)
    flip = {"chase": (0.45, -1.0, 0.5), "front34": (0.85, 1.0, 0.35), "side": (1.0, 0.0, 0.08),
            "top": (0.0, 0.001, 1.0), "under": (0.35, -0.6, -0.7)}
    R.VIEWS.update(flip)
    for v in ("front34", "chase", "side", "top"):
        out = os.path.join(out_dir, f"bee_{v}.png")
        R.render_frame(out, v, meshes, res=(700, 560), samples=24)
        files.append(out)
    sheet = os.path.join(out_dir, "bee_contact_sheet.png")
    R.compose(files, sheet, cols=2)
    return sheet


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUTPUT)
    ap.add_argument("--render")
    a = ap.parse_args(argv)
    objects = build()
    print(f"bee model: {len(objects)} objects, {tri_count(objects)} triangles")
    export(objects, a.out)
    print(f"bee model: wrote {os.path.relpath(a.out, REPO)}")
    if a.render:
        print("bee model: contact sheet", render(a.out, a.render))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]))
