#!/usr/bin/env python3
"""Is this creature's SILHOUETTE its own? Every fauna body and every vessel hull, compared shape against shape.

    python3 Tools/Build/fauna_kit/silhouette.py                 # each body's nearest look-alike, and the closest pairs
    python3 Tools/Build/fauna_kit/silhouette.py --check         # exit 1 when a NEW species is too like something else
    python3 Tools/Build/fauna_kit/silhouette.py --sheet <png>   # every silhouette, from the side and from ahead
    python3 Tools/Build/fauna_kit/silhouette.py --self-test

Garrett, 2026-11-16: "the fauna and vessels need to have unique silhouettes so they are all distinguished."

A silhouette here is the body seen from far away with no detail: its points (model vertices, braid vertices, prism
corners) flattened onto a 40 x 40 grid, scaled so the body's longest extent fills it (one scale for both views), and thickened by one cell.
Two views: from the SIDE (travel axis across the picture) and from AHEAD (down the travel axis). A HyperSea body has
no up, so a body is tried at every roll about its travel axis (and nose to tail), and the difference between two
bodies is the SMALLEST they can be made to show: 1 - (cells both fill / cells either fills), averaged over the two
views. 0 = the same outline, 1 = nothing shared. Size is ignored on purpose: a small copy of a shape is not a new shape.

A READER. The bodies are the kit's own (body.from_spec for every fauna spec; the vessel model files for the hulls).
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import body as BODY  # noqa: E402
import celestial as C  # noqa: E402
import dossier as D  # noqa: E402
import fbx_model as fm  # noqa: E402
import recreate as R  # noqa: E402

N = 40
ROLLS = 8                 # side views tried, over half a turn
SPINS = 16                # ahead views tried, over a full turn
VESSELS = "Assets/_Models/Vessel Models"
# the hull each vessel class flies (Docs/VesselModels/README.md's table: the file its prefab draws)
HULLS = {"Angler": "Angler.fbx", "Ark": "ArkReverse.fbx", "Brittle Star": "BrittleStar.fbx", "Butterfly": "Butterfly.fbx",
         "Dolphin": "dolphin_shapekey_with_animations.fbx", "Goby": "Goby.fbx", "Grizzly": "Grizzly.fbx", "Grouper": "Grouper.fbx",
         "Honeyguide": "Honeyguide.fbx", "Jack": "Jack.fbx", "Jewel Wasp": "JewelWasp.fbx", "Manta": "Manta_shapekey_rigged.fbx",
         "Marlin": "Marlin.fbx", "Meerkat": "Meerkat.fbx", "Orca": "Orca.fbx", "Pistol": "Pistol.fbx",
         "Rhino": "rhino_shapekey_with_animations.fbx", "Scarab": "Scarab.fbx", "Serpent": "serpent.fbx", "Shrike": "Shrike.fbx",
         "Sparrow": "SparrowModel4.fbx", "Squirrel": "SquirrelVessel_CosmicShoresTest1.fbx", "Starling": "Starling.fbx",
         "Stoat": "Stoat.fbx", "Swordfish": "Swordfish.fbx", "Thresher": "Thresher.fbx",
         "Urchin": "urchan_shapekey_with_animations.fbx", "Wrasse Kite": "WrasseKite.fbx"}
# The yardstick is the shipped set itself, measured each run: every shipped body's distance to its nearest shipped
# look-alike (2026-11-16, 28 hulls and 7 creatures: 0.10 to 0.46, median 0.25). A NEW species must stand further from
# every vessel, every shipped creature and every other new species than three in four shipped bodies stand from
# theirs (the upper quartile). Archived proposals are listed and count for nobody. No constant to tune: add a hull
# and the bar moves with the fleet. ALIKE is only the self-test's line between its two planted shapes.
ALIKE = 0.30
MIN_DISTANCE = ALIKE


def body_points(body):
    pts = [v for w in body["wires"] for v in w["verts"]]
    for p in body["prisms"]:
        m = fm.trs(p["at"], p["q"], [s / 2 for s in p["size"]])
        for sx in (-1, 0, 1):
            for sy in (-1, 0, 1):
                for sz in (-1, -0.5, 0, 0.5, 1):
                    pts.append(fm.mpoint(m, [sx, sy, sz]))
    return np.array(pts, dtype=float)


def worm_points(spec):
    """A worm colony as it swims: its head, six bodies and tail in a line (WormFauna's spacing and taper)."""
    n = spec["numbers"]
    made = BODY.from_spec(spec)["segments"]
    pts, z, count = [], 0.0, int(max(3, n["spawn_segment_count"]))
    for i in range(count):
        role = "head" if i == 0 else ("tail" if i == count - 1 else "body")
        sc = n["kaiju_scale"] * n["taper_per_segment"] ** i
        if i > 0:
            gap = n["segment_spacing"] * n["kaiju_scale"] * n["taper_per_segment"] ** max(0, i - 2)
            gap *= n["head_gap_multiplier"] if i == 1 else 1.0
            gap *= n["tail_gap_multiplier"] if i == count - 1 else 1.0
            z -= gap
        p = body_points(made[role]) * sc
        p[:, 2] += z
        pts.append(p)
    return np.concatenate(pts)


def vessel_points(file):
    model = fm.load(os.path.join(D.REPO, VESSELS, file))
    pts = []
    for me in model["meshes"]:
        w = model["nodes"][me["node"]]["world"]
        pts += [fm.mpoint(w, v) for v in me["verts"][::max(1, len(me["verts"]) // 6000)]]
    return np.array(pts, dtype=float)


def roster():
    """[(name, kind, points)]: kind is 'vessel', 'fauna' (shipped), 'new' or 'archive' (a superseded proposal)."""
    import lab_export
    out = []
    for name, file in HULLS.items():
        if os.path.exists(os.path.join(D.REPO, VESSELS, file)):
            out.append((name + " (vessel)", "vessel", vessel_points(file)))
    for key in D.ROSTER:
        spec = R.load_spec(key)
        pts = worm_points(spec) if "segments" in spec else body_points(BODY.from_spec(spec))
        out.append((D.ROSTER[key]["name"], "fauna", pts))
    for key in lab_export.NEW_SPECIES:
        out.append((C.load(key)["species"], "new", body_points(BODY.from_spec(C.load(key)))))
    for key in lab_export.ARCHIVED:
        out.append((C.load(key)["species"], "archive", body_points(BODY.from_spec(C.load(key)))))
    return out


def _mask(a, b, span):
    """Two coordinate rows -> a filled N x N boolean grid, thickened one cell. `span` is the BODY's longest extent,
    the same for every view of it: fitted per view, a dart seen end-on fills the grid and reads as a disc (the
    self-test's control caught exactly that)."""
    ia = np.clip(((a - (a.min() + a.max()) / 2) / span + 0.5) * (N - 1), 0, N - 1).round().astype(int)
    ib = np.clip(((b - (b.min() + b.max()) / 2) / span + 0.5) * (N - 1), 0, N - 1).round().astype(int)
    g = np.zeros((N + 2, N + 2), dtype=bool)
    g[ib + 1, ia + 1] = True
    g = g | np.roll(g, 1, 0) | np.roll(g, -1, 0) | np.roll(g, 1, 1) | np.roll(g, -1, 1)
    return g[1:-1, 1:-1]


def views(pts):
    """{'side': [masks at each roll, nose either way], 'ahead': [masks at each spin]}"""
    side, ahead = [], []
    span = float(max((pts.max(0) - pts.min(0)).max(), 1e-9)) * 1.05
    for perm in ((0, 1, 2), (1, 2, 0), (2, 0, 1)):      # a model file's travel axis is not always z: try each
        q = pts[:, perm]
        _views_into(q, side, ahead, span)
    return {"side": np.array(side), "ahead": np.array(ahead)}


def _views_into(pts, side, ahead, span):
    for k in range(ROLLS):
        t = np.pi * k / ROLLS
        y = pts[:, 0] * np.sin(t) + pts[:, 1] * np.cos(t)
        m = _mask(pts[:, 2], y, span)
        side += [m, m[:, ::-1], m[::-1, :], m[::-1, ::-1]]
    for k in range(SPINS):
        t = 2 * np.pi * k / SPINS
        x = pts[:, 0] * np.cos(t) - pts[:, 1] * np.sin(t)
        y = pts[:, 0] * np.sin(t) + pts[:, 1] * np.cos(t)
        ahead.append(_mask(x, y, span))


def _best(A, B):
    """The smallest 1 - IoU over every pairing of A's masks with B's."""
    a = A.reshape(len(A), -1).astype(np.float32)
    b = B.reshape(len(B), -1).astype(np.float32)
    inter = a @ b.T
    union = a.sum(1)[:, None] + b.sum(1)[None, :] - inter
    return float(1.0 - (inter / np.maximum(union, 1.0)).max())


def distance(va, vb):
    return 0.5 * (_best(va["side"], vb["side"]) + _best(va["ahead"][:1], vb["ahead"]))


def table():
    items = roster()
    vs = [views(p) for _n, _k, p in items]
    n = len(items)
    d = np.ones((n, n))
    for i in range(n):
        for j in range(i + 1, n):
            d[i, j] = d[j, i] = distance(vs[i], vs[j])
    return items, vs, d


def sheet(items, vs, out):
    from PIL import Image, ImageDraw
    cell, pad, cols = N * 3, 26, 6
    rows = (len(items) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cell * 2 + 14) + 10, rows * (cell + pad) + 30), (14, 18, 30))
    dr = ImageDraw.Draw(img)
    dr.text((8, 6), "silhouettes: from the side (left) and from ahead (right); vessels grey, shipped fauna blue, new fauna green, archived proposals violet", fill=(210, 215, 225))
    col = {"vessel": (170, 176, 188), "fauna": (110, 160, 255), "new": (120, 255, 190), "archive": (150, 120, 190)}
    for i, (name, kind, _p) in enumerate(items):
        x0, y0 = 10 + (i % cols) * (cell * 2 + 14), 28 + (i // cols) * (cell + pad)
        dr.text((x0, y0), name, fill=col[kind])
        for j, m in enumerate((vs[i]["side"][0], vs[i]["ahead"][0])):
            im = Image.fromarray((m[::-1] * 255).astype(np.uint8)).resize((cell, cell), Image.NEAREST)
            tint = Image.new("RGB", (cell, cell), col[kind])
            img.paste(tint, (x0 + j * (cell + 4), y0 + 12), im)
    img.save(out)
    return out


def self_test():
    fails = []
    rng = np.random.default_rng(1)
    dart = np.concatenate([np.c_[rng.normal(0, 0.3, 400), rng.normal(0, 0.3, 400), rng.uniform(-20, 0, 400)]])
    disc = np.c_[rng.uniform(-10, 10, 1500), rng.uniform(-10, 10, 1500), rng.normal(0, 0.2, 1500)]
    disc = disc[(disc[:, 0] ** 2 + disc[:, 1] ** 2) < 100]
    rolled = dart.copy()
    rolled[:, [0, 1]] = np.c_[dart[:, 1], -dart[:, 0]] * 3.0       # the same dart, rolled a quarter turn and three times the size
    rolled[:, 2] = -dart[:, 2] * 3.0                                # and nose to tail
    if distance(views(dart), views(rolled)) > 0.15:
        fails.append(f"a rolled, flipped, scaled copy reads as a different shape ({distance(views(dart), views(rolled)):.2f})")
    if distance(views(dart), views(disc)) < MIN_DISTANCE:
        fails.append(f"a dart and a disc read as alike ({distance(views(dart), views(disc)):.2f})")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("silhouette self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    items, vs, d = table()
    names = [n for n, _k, _p in items]
    bad = 0
    shipped = [i for i, (_n, k, _p) in enumerate(items) if k in ("vessel", "fauna")]
    nearest = [min(d[i, j] for j in shipped if j != i) for i in shipped]
    bar = float(np.percentile(nearest, 75))
    print(f"shipped bodies stand {min(nearest):.2f} to {max(nearest):.2f} from their nearest look-alike (median "
          f"{np.median(nearest):.2f}); the bar for a new species is the upper quartile, {bar:.2f}")
    print(f"{'body':<26} {'kind':<7} {'nearest look-alike':<26} {'distance':>8}")
    live = np.array([k != "archive" for _n, k, _p in items])
    for i, (name, kind, _p) in enumerate(items):
        row = np.where(live | (kind == "archive"), d[i], 9.0)      # an archived proposal is nobody's neighbour
        row[i] = 9.0
        j = int(np.argmin(row))
        flag = ""
        if kind == "new" and d[i, j] < bar:
            flag = "  TOO ALIKE"
            bad += 1
        print(f"{name:<26} {kind:<7} {names[j]:<26} {d[i, j]:>8.2f}{flag}")
    pairs = sorted((d[i, j], names[i], names[j]) for i in range(len(items)) for j in range(i + 1, len(items)) if live[i] and live[j])
    print("closest pairs:", "; ".join(f"{a} / {b} {v:.2f}" for v, a, b in pairs[:8]))
    if "--sheet" in argv:
        print("sheet:", sheet(items, vs, argv[argv.index("--sheet") + 1]))
    if "--check" in argv and bad:
        print(f"{bad} new species stand closer than {bar:.2f} to something the game already has")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
