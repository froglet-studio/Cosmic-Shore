#!/usr/bin/env python3
"""Render a BUILT celestial fauna prefab offline (no Unity, no Blender): a four-view sheet of what the prefab poses.

    python3 Tools/Build/fauna_kit/render_prefab.py bee Assets/_Prefabs/FloraAndFauna/Bees/BeeFauna.prefab --out <png>

Unlike preview.py (which draws the SPEC), this reads the PREFAB: every spindle's and prism's world pose comes from the
prefab's own transforms and prism-instance overrides (build.read_back), every braid from its `Braid` child's local
position and scale in the prefab, and every braid is the real mesh (`bonita.fbx` `BezierCurve.001`, its triangles read
with fbx_binary, the FBX's x mirrored into Unity's left-handed frame). Prisms are their boxes, danger in red; the heart is
a stand-in octahedron of the one heart size (3.55) at the origin. Flat-lit, painter's order: a look at the geometry, not
the game's shaders. A READER: it writes only the PNG.
"""
import math
import os
import re
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))   # Tools/Build/fauna_kit -> the repo
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import build as B          # noqa: E402
import celestial as C      # noqa: E402
import fbx_binary          # noqa: E402

BG, BRAID, PRISM, DANGER, HEART, TEXT = (14, 18, 30), (150, 178, 215), (226, 231, 240), (255, 72, 92), (92, 232, 255), (200, 205, 215)
LIGHT = C.unit([0.4, 0.7, -0.6])


def braid_mesh():
    """The braid's vertices (Unity units, Unity's frame) and triangles."""
    nodes, _v, _f = fbx_binary.read(os.path.join(REPO, "Assets", "_Models", "Fauna", "bonita.fbx"))
    objs = [t for t in nodes if t.name == "Objects"][0]
    for g in objs.find("Geometry"):
        name = [x for _, x in g.props][1]
        if (name if isinstance(name, str) else name.decode("latin1")).startswith("BezierCurve.001"):
            arr = [x for _, x in g.first("Vertices").props][0]
            idx = [x for _, x in g.first("PolygonVertexIndex").props][0]
            verts = [[-arr[i] / 100, arr[i + 1] / 100, arr[i + 2] / 100] for i in range(0, len(arr), 3)]
            tris, poly = [], []
            for i in idx:
                if i < 0:
                    poly.append(~i)
                    for k in range(1, len(poly) - 1):
                        tris.append((poly[0], poly[k], poly[k + 1]))
                    poly = []
                else:
                    poly.append(i)
            return verts, tris
    raise SystemExit("bonita.fbx: no BezierCurve.001 geometry")


def braid_locals(prefab):
    """Each spindle name -> its Braid child's (local position, uniform scale), read from the prefab."""
    docs = {m.group(2): d for d in re.split(r"(?m)^(?=--- !u!)", prefab) for m in [re.match(r"--- !u!(\d+) &(-?\d+)", d)] if m}
    out = {}
    for fid, d in docs.items():
        if d.startswith("--- !u!1 &") and "  m_Name: Braid\n" in d:
            tr = re.search(r"  - component: \{fileID: (-?\d+)\}", d).group(1)
            t = docs[tr]
            father = re.search(r"  m_Father: \{fileID: (-?\d+)\}", t).group(1)
            go = re.search(r"  m_GameObject: \{fileID: (-?\d+)\}", docs[father]).group(1)
            name = re.search(r"  m_Name: (\S+)\n", docs[go]).group(1)
            pos = [float(v) for v in re.search(r"m_LocalPosition: \{x: (\S+), y: (\S+), z: (\S+)\}", t).groups()]
            sc = float(re.search(r"m_LocalScale: \{x: (\S+),", t).group(1))
            out[name] = (pos, sc)
    return out


def scene(spec, prefab):
    world, problems = B.read_back(prefab, spec)
    if problems:
        raise SystemExit("the prefab is not its spec: " + "; ".join(problems))
    verts, tris = braid_mesh()
    braids = braid_locals(prefab)
    faces = []   # (points, base colour)
    for s in spec["spindles"]:
        pos, rot = world[s["name"]]
        lp, sc = braids[s["name"]]
        for a, b, c in tris:
            pts = [C.add(pos, B.qrot(rot, C.add(lp, C.mul(verts[i], sc)))) for i in (a, b, c)]
            faces.append((pts, BRAID))
    for p in spec["prisms"]:
        at, rot = world[p["name"]]
        h = [v / 2 for v in p["size"]]
        corners = [C.add(at, B.qrot(rot, [sx * h[0], sy * h[1], sz * h[2]])) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        col = DANGER if p["kind"] == "danger" else PRISM
        for q in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            faces.append(([corners[i] for i in q], col))
    r = C.HEART_WORLD / 2
    o = [[r, 0, 0], [-r, 0, 0], [0, r, 0], [0, -r, 0], [0, 0, r], [0, 0, -r]]
    for t in ((0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)):
        faces.append(([o[i] for i in t], HEART))
    return faces


def view(yaw, pitch):
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    def proj(p):
        x = cy * p[0] + sy * p[2]
        z = -sy * p[0] + cy * p[2]
        return (x, cp * p[1] - sp * z, sp * p[1] + cp * z)
    return proj


def draw(faces, proj, size, title):
    img = Image.new("RGB", (size, size), BG)
    g = ImageDraw.Draw(img)
    items = []
    for pts, col in faces:
        pp = [proj(q) for q in pts]
        n = C.cross(C.sub(pts[1], pts[0]), C.sub(pts[2], pts[0]))
        shade = 0.45 + 0.55 * abs(C.dot(C.unit(n), LIGHT)) if C.norm(n) > 1e-12 else 0.6
        items.append((sum(q[2] for q in pp) / len(pp), [(q[0], q[1]) for q in pp], tuple(int(c * shade) for c in col)))
    xs = [q[0] for it in items for q in it[1]]
    ys = [q[1] for it in items for q in it[1]]
    span = max(max(xs) - min(xs), max(ys) - min(ys), 1e-6)
    sc, cx, cy = size * 0.84 / span, (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    for _d, pts, col in sorted(items, key=lambda it: -it[0]):
        g.polygon([(size / 2 + (x - cx) * sc, size / 2 - (y - cy) * sc) for x, y in pts], fill=col)
    g.text((10, 8), title, fill=TEXT)
    return img


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    spec = C.load(argv[0])
    prefab = open(os.path.join(REPO, argv[1]) if not os.path.isabs(argv[1]) else argv[1], encoding="utf-8").read()
    out = argv[argv.index("--out") + 1] if "--out" in argv else f"{argv[0]}_built.png"
    faces = scene(spec, prefab)
    size = 560
    views = [("from ahead", view(math.pi, 0.0)), ("side", view(math.pi / 2, 0.0)),
             ("three-quarters", view(math.radians(140), math.radians(25))), ("from behind", view(0.0, 0.0))]
    sheet = Image.new("RGB", (size * 2, size * 2), BG)
    for k, (title, proj) in enumerate(views):
        sheet.paste(draw(faces, proj, size, f"{spec['species']} prefab (built) - {title}"), ((k % 2) * size, (k // 2) * size))
    sheet.save(out)
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
