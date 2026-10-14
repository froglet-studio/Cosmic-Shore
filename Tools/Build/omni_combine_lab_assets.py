#!/usr/bin/env python3
"""
Bakes the Omni Combine Lab's data block (Docs/Studios/OmniCombineLab.html, between the lines `// <assets>`
and `// </assets>`), and the same block into the Omni Motion Lab (Docs/Studios/OmniMotionLab.html), which reads
the shipped omni and the four effects' numbers from it:

  - the artist's combine targets, `Docs/Studios/data/omni_crystal_combine.json`, baked out of
    CrystalsRaw.blend by `Tools/Build/bake_omni_crystal_combine.py`: each elemental crystal's Basis and
    Key 1, its triangles and plates, and the omni vertex every Key 1 vertex lands on;
  - the colour pair every crystal ships in: `_BrightColor` / `_DarkColor` of `OmniCrystalBody.mat`, checked
    against each elemental crystal's own material (`_BrightColor`/`_DarkColor`, or Charge's
    `_BrightCrystalColor`/`_DullCrystalColor`), so the page says so when one of them moves.

The lab only READS these. Coordinates stay Blender's (Z up); the page converts to Y up.

    python3 Tools/Build/omni_combine_lab_assets.py            # rewrite the block in the lab
    python3 Tools/Build/omni_combine_lab_assets.py --check    # exit 1 if the lab's block is stale
    python3 Tools/Build/omni_combine_lab_assets.py --self-test
"""

import argparse
import json
import os
import re
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LAB = os.path.join(ROOT, "Docs", "Studios", "OmniCombineLab.html")
# Every page that carries this block. The Omni Motion Lab (2026-10-13) reuses it unchanged.
LABS = [LAB, os.path.join(ROOT, "Docs", "Studios", "OmniMotionLab.html")]
DATA = os.path.join(ROOT, "Docs", "Studios", "data", "omni_crystal_combine.json")
SQUARES = os.path.join(ROOT, "Docs", "Studios", "data", "omni_space_squares.json")
DEPTH_PROFILE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "CrystalDepthPrismProfile.asset")
FLIP_PROFILE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "TimeCrystalFlipWaveProfile.asset")
MATS = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "CrystalMaterials")
PREFABS = os.path.join(ROOT, "Assets", "_Prefabs", "Environment")
ELEMENT_PREFABS = {"Charge": "CrystalCharge", "Mass": "CrystalMass", "Space": "CrystalSpace", "Time": "CrystalTime"}
OPEN, CLOSE = "// <assets>", "// </assets>"


def read(path):
    with open(path) as f:
        return f.read()


def guid_index():
    out = {}
    for base in ("Assets/_Graphics", "Assets/_Prefabs/Environment"):
        for dirpath, _, files in os.walk(os.path.join(ROOT, base)):
            for name in files:
                if name.endswith(".meta"):
                    m = re.search(r"^guid: (\w+)", read(os.path.join(dirpath, name)), re.M)
                    if m:
                        out[m.group(1)] = os.path.join(dirpath, name[:-5])
    return out


def colour(text, *names):
    for name in names:
        m = re.search(r"^    - " + name + r": \{r: ([-0-9.e]+), g: ([-0-9.e]+), b: ([-0-9.e]+), a: ([-0-9.e]+)\}", text, re.M)
        if m:
            return [round(float(x), 6) for x in m.groups()]
    return None


def pair(text):
    bright = colour(text, "_BrightColor", "_BrightCrystalColor")
    dark = colour(text, "_DarkColor", "_DullCrystalColor")
    return (bright, dark) if bright and dark else None


def shipped_colours():
    omni = pair(read(os.path.join(MATS, "OmniCrystalBody.mat")))
    if not omni:
        raise SystemExit("OmniCrystalBody.mat lacks _BrightColor/_DarkColor")
    guids, elements = guid_index(), {}
    for element, prefab in ELEMENT_PREFABS.items():
        text = read(os.path.join(PREFABS, prefab + ".prefab"))
        m = re.search(r"  - model: \{fileID: -?\d+\}\n    defaultMaterial: \{fileID: \d+, guid: (\w+)", text)
        if not m or m.group(1) not in guids:
            raise SystemExit(f"{prefab}.prefab: slot 0 has no resolvable defaultMaterial")
        path = guids[m.group(1)]
        got = pair(read(path))
        elements[element] = {"material": os.path.basename(path)[:-4], "bright": got[0] if got else None,
                             "dark": got[1] if got else None}
    return {"omni": {"bright": omni[0], "dark": omni[1]}, "elements": elements}


def validate(d):
    """The combine data must land: every crystal's arrays agree, and the four cover every omni vertex."""
    nv = len(d["omni"]["verts"]) // 3
    covered = set()
    for c in d["crystals"]:
        n = len(c["basis"]) // 3
        if len(c["key1"]) != 3 * n or len(c["lands"]) != n or len(c["plate"]) != n:
            raise SystemExit(f"{c['element']}: basis/key1/lands/plate disagree on the vertex count")
        if max(c["tris"]) >= n:
            raise SystemExit(f"{c['element']}: a triangle names a vertex the crystal does not have")
        if max(c["lands"]) >= nv:
            raise SystemExit(f"{c['element']}: lands on a vertex the omni does not have")
        covered.update(c["lands"])
    if len(covered) != nv:
        raise SystemExit(f"the crystals land on {len(covered)} of the omni's {nv} vertices")


# ---------------------------------------------------------------- derived targets (round 2)
# The designer's morph (2026-10-13): Space first; Charge's petals suck their three-edge back in to one edge,
# then the five untwist while their tips fuse at the pentagon's centre and flatten, so each petal becomes ONE
# FIFTH of the pentagon; Mass's three prisms get the same fuse-and-flatten into thirds of the triangle; Time
# locks in rigidly, no morph. These targets are derived from the art file's own geometry, never hand-placed.
SITE_KINDS = {"Charge": 5, "Mass": 3}


def _arr(flat):
    return np.array(flat, dtype=float).reshape(-1, 3)


def _frame(axis):
    a = axis / np.linalg.norm(axis)
    r = np.cross(a, [0.123, 0.456, 0.789])
    r /= np.linalg.norm(r)
    return a, r, np.cross(a, r)


def _az(v, frame):
    a, r, r2 = frame
    return np.degrees(np.arctan2(v @ r2, v @ r)) % 360


def _wrap(x):
    return (x + 180) % 360 - 180


def _layers(points, idx):
    """Split a slab's vertices into its outer and inner layer by radius."""
    rad = np.linalg.norm(points[idx], axis=1)
    mid = (rad.max() + rad.min()) / 2
    return [i for i, r in zip(idx, rad) if r > mid], [i for i, r in zip(idx, rad) if r <= mid]


def wedge_targets(d, element):
    """Per vertex of `element`: its stage-1 position (Charge: the back sucked onto the shoulders' line, in the
    crystal's own frame) and its final position (one wedge of its omni plate, in the omni's frame), plus each
    site's twist. Refuses wedges that do not tile each omni face exactly once."""
    c = next(x for x in d["crystals"] if x["element"] == element)
    n_sides = SITE_KINDS[element]
    B, O = _arr(c["basis"]), _arr(d["omni"]["verts"])
    oplate = d["omni"]["plate"]
    plates, sites = {}, {}
    for i, p in enumerate(c["plate"]):
        plates.setdefault(p, []).append(i)
    for p, idx in plates.items():
        sites.setdefault(oplate[c["lands"][idx[0]]], []).append(p)
    omni_of = {}
    for j, p in enumerate(oplate):
        omni_of.setdefault(p, []).append(j)
    stage1, final = B.copy(), np.zeros_like(B)
    twists, worst_plane = [], 0.0
    plate_twist, plate_axis = [0.0] * len(plates), [None] * len(plates)
    for site, petals in sites.items():
        if len(petals) != n_sides:
            raise SystemExit(f"{element}: omni plate {site} gathers {len(petals)} plates, expected {n_sides}")
        overts = omni_of[site]
        frame = _frame(O[overts].mean(axis=0))
        o_layers = _layers(O, overts)
        # Petal axes (azimuth of each petal's tip-to-back direction) and the omni plate's side midpoints.
        info = []
        for p in petals:
            out, _ = _layers(B, plates[p])
            pts = B[out]
            off = np.degrees(np.arccos(np.clip((pts / np.linalg.norm(pts, axis=1)[:, None]) @ frame[0], -1, 1)))
            tip = out[int(np.argmin(off))]
            rest = [i for i in out if i != tip]
            phi = _az(B[rest].mean(axis=0), frame)
            info.append((p, phi))
        corners = sorted(o_layers[0], key=lambda j: _az(O[j], frame))
        caz = [_az(O[j], frame) for j in corners]
        mids = [(caz[k] + _wrap(caz[(k + 1) % n_sides] - caz[k]) / 2) % 360 for k in range(n_sides)]
        best = None
        for k in range(n_sides):
            delta = _wrap(mids[k] - info[0][1])
            assign = {}
            for p, phi in info:
                m = int(np.argmin([abs(_wrap(phi + delta - mm)) for mm in mids]))
                if abs(_wrap(phi + delta - mids[m])) > 3:
                    break
                assign[p] = m
            else:
                if len(set(assign.values())) == n_sides and (best is None or abs(delta) < abs(best[0])):
                    best = (delta, assign)
        if best is None:
            raise SystemExit(f"{element}: the plates at omni plate {site} do not untwist onto its sides")
        delta, assign = best
        twists.append(delta)
        for p in petals:
            plate_twist[p], plate_axis[p] = round(float(delta), 3), [round(float(x), 6) for x in frame[0]]
        for p, phi in info:
            side = assign[p]
            for layer in (0, 1):
                mine = _layers(B, plates[p])[layer]
                ocs = sorted(o_layers[layer], key=lambda j: _az(O[j], frame))
                cA, cB = O[ocs[side]], O[ocs[(side + 1) % n_sides]]
                centre = O[o_layers[layer]].mean(axis=0)
                pts = B[mine]
                off = np.degrees(np.arccos(np.clip((pts / np.linalg.norm(pts, axis=1)[:, None]) @ frame[0], -1, 1)))
                tip = mine[int(np.argmin(off))]
                rest = sorted((i for i in mine if i != tip), key=lambda i: _wrap(_az(B[i], frame) - phi))
                shoulders = [rest[0], rest[-1]]                  # the two that reach furthest round
                sL, sR = B[shoulders[0]], B[shoulders[1]]
                final[tip] = centre
                final[shoulders[0]], final[shoulders[1]] = cA, cB
                for i in rest[1:-1]:                              # Charge's back: onto the shoulders' line
                    u = float(np.clip((B[i] - sL) @ (sR - sL) / ((sR - sL) @ (sR - sL)), 0, 1))
                    stage1[i] = sL + u * (sR - sL)
                    final[i] = cA + u * (cB - cA)
                normal = np.cross(cA - centre, cB - centre)
                normal /= np.linalg.norm(normal)
                for i in mine:
                    worst_plane = max(worst_plane, abs((final[i] - centre) @ normal))
        # Tiling: per layer, the wedges' areas add up to the face's, and every side is used once.
        for layer in (0, 1):
            ocs = O[sorted(o_layers[layer], key=lambda j: _az(O[j], frame))]
            centre = ocs.mean(axis=0)
            face = sum(np.linalg.norm(np.cross(ocs[k] - centre, ocs[(k + 1) % n_sides] - centre)) / 2 for k in range(n_sides))
            wedges = 0.0
            for p in petals:
                mine = _layers(B, plates[p])[layer]
                pts = final[mine]
                tip = mine[int(np.argmin(np.linalg.norm(final[mine] - centre, axis=1)))]
                others = [final[i] for i in mine if i != tip]
                far = max(((a, b) for a in others for b in others), key=lambda ab: np.linalg.norm(ab[0] - ab[1]))
                wedges += np.linalg.norm(np.cross(far[0] - centre, far[1] - centre)) / 2
            if abs(wedges - face) > 1e-4 * face:
                raise SystemExit(f"{element}: wedges at omni plate {site} cover {wedges:.5f} of a face of {face:.5f}")
    if worst_plane > 1e-5:
        raise SystemExit(f"{element}: a wedge target leaves its omni face by {worst_plane:.2e}")
    return stage1, final, twists, plate_twist, plate_axis


def time_rigid(d):
    """Time locks in without a morph: per rhombus, the rotation (a quaternion, w x y z, Blender axes) that
    carries its Basis shape onto its slot (Kabsch), and the rigid final pose. The fit is robust: a corner
    whose Key 1 disagrees with the rest is left out and reported, because the art file has one (rhombus 2's
    Key 1 swaps corners 9 and 129 between its inner and outer faces). Refuses a rigid pose that does not put
    every corner on a distinct omni vertex."""
    c = next(x for x in d["crystals"] if x["element"] == "Time")
    B, K, O = _arr(c["basis"]), _arr(c["key1"]), _arr(d["omni"]["verts"])
    plates = {}
    for i, p in enumerate(c["plate"]):
        plates.setdefault(p, []).append(i)

    def kabsch(b, k):
        u, _, vt = np.linalg.svd((b - b.mean(axis=0)).T @ (k - k.mean(axis=0)))
        R = vt.T @ np.diag([1, 1, np.sign(np.linalg.det(vt.T @ u.T))]) @ u.T
        return R, k.mean(axis=0) - R @ b.mean(axis=0)
    quats, final, slips, worst = [], np.zeros_like(B), [], 0.0
    for p in sorted(plates):
        idx = plates[p]
        R, tr = kabsch(B[idx], K[idx])
        err = np.linalg.norm((R @ B[idx].T).T + tr - K[idx], axis=1)
        bad = [i for i, e in zip(idx, err) if e > 4 * np.median(err) + 1e-3]
        if bad:
            good = [i for i in idx if i not in bad]
            R, tr = kabsch(B[good], K[good])
            slips.append({"rhombus": p, "vertices": bad})
        final[idx] = (R @ B[idx].T).T + tr
        w = np.sqrt(max(0.0, 1 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
        quats.append([round(float(x), 6) for x in (w, (R[2, 1] - R[1, 2]) / (4 * w), (R[0, 2] - R[2, 0]) / (4 * w), (R[1, 0] - R[0, 1]) / (4 * w))])
    hits = []
    for i in range(len(B)):
        dist = np.linalg.norm(O - final[i], axis=1)
        j = int(np.argmin(dist))
        hits.append(j)
        worst = max(worst, float(dist[j]))
    if worst > 0.012 or len(set(hits)) != len(hits):
        raise SystemExit(f"Time: the rigid lock-in misses the omni (worst {worst:.3f}, {len(set(hits))}/{len(hits)} distinct)")
    return quats, final, worst, slips


def omni_effects(d):
    """What the finished omni does (the game's four effects on the omni), read from the shipped data:
    which element owns each omni plate (by which crystal lands on it), the Space squares weave
    (omni_space_squares.json, baked from CrystalsRaw_V2.blend, mapped onto these omni vertices), and the
    depth-prism numbers (CrystalDepthPrismProfile.asset, OmniDepthPrisms.mat / MassDepthPrisms.mat)."""
    O = _arr(d["omni"]["verts"])
    owner = [None] * len(d["omni"]["plate"])
    for c in d["crystals"]:
        for j in c["lands"]:
            owner[j] = c["element"]
    if None in owner:
        raise SystemExit("an omni vertex has no owning element")
    plate_owner = {}
    for j, p in enumerate(d["omni"]["plate"]):
        if plate_owner.setdefault(p, owner[j]) != owner[j]:
            raise SystemExit(f"omni plate {p} is claimed by two elements")
    sq = json.loads(read(SQUARES))
    Bq = np.array(sq["basis"], dtype=float).reshape(-1, 3)
    s = np.linalg.norm(O, axis=1).max() / np.linalg.norm(Bq, axis=1).max()
    to_omni = []
    for v in Bq * s:
        dist = np.linalg.norm(O - v, axis=1)
        j = int(np.argmin(dist))
        if dist[j] > 1e-3:
            raise SystemExit(f"the squares data does not land on the omni ({dist[j]:.2e})")
        to_omni.append(j)
    keys = [{"name": k["name"], "relative": k["relative"], "verts": [to_omni[v] for v in k["verts"]],
             "delta": [round(x * s, 5) for x in k["delta"]]} for k in sq["keys"]]
    floats = lambda text: {k: float(v) for k, v in re.findall(r"^\s+- (_\w+): (-?[0-9.eE+-]+)$", text, re.M)}
    prof = {k: float(v) for k, v in re.findall(r"^  (\w+): (-?[0-9.eE+-]+)$", read(DEPTH_PROFILE), re.M)}
    depth = {"open": prof["openDepth"], "seal": prof["sealDepth"], "power": prof["profilePower"]}
    for name, mat in (("omni", "OmniDepthPrisms"), ("mass", "MassDepthPrisms")):
        f = floats(read(os.path.join(MATS, mat + ".mat")))
        depth[name] = {"reach": f["_Reach"], "sizeStart": f["_PlateScaleStart"], "faceSplit": f["_FaceSplit"]}
    # The prisms' thickness is baked into their meshes by the generator that owns them; import it, never copy it
    # (labmaker L-STU-40). The Omni Motion Lab thickens the art omni's triangle plates by this, inward.
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import author_omni_crystal_triangles as omni_tris  # noqa: E402
    depth["omni"]["thickness"] = float(omni_tris.DEPTH_THICKNESS)
    return {"plateOwner": [plate_owner[p] for p in range(len(plate_owner))],
            "squares": {"fps": sq["fps"], "keys": keys, "weights": sq["weights"]}, "depth": depth}


def flip_profile():
    """TimeCrystalFlipWaveProfile.asset as the game plays it (FlipWaveProfileSO.Sample): the loop, the ring
    starts, and every curve's keys (time, value) - flip fraction, scale, dip and slide, for the lead ring and
    the later rings. An empty curve is returned empty (the game evaluates it as 1 / 0 / 0)."""
    text = read(FLIP_PROFILE)
    head = {k: float(v) for k, v in re.findall(r"^  (loopSeconds|firstRingStartSeconds|ringStaggerSeconds): (\S+)$", text, re.M)}
    curves, ring, curve, key_t = {}, None, None, None
    for line in text.splitlines():
        m = re.match(r"^  (leadRing|laterRings):\s*$", line)
        if m:
            ring = m.group(1)
            continue
        m = re.match(r"^    (flip|scale|dip|slide):\s*$", line)
        if m and ring:
            curve = m.group(1)
            curves.setdefault(ring, {})[curve] = []
            continue
        m = re.match(r"^        time: (\S+)$", line)
        if m and ring and curve:
            key_t = float(m.group(1))
            continue
        m = re.match(r"^        value: (\S+)$", line)
        if m and key_t is not None:
            curves[ring][curve].append([round(key_t, 4), round(float(m.group(1)), 6)])
            key_t = None
    if len(head) != 3 or not curves.get("leadRing", {}).get("flip") or not curves.get("laterRings", {}).get("flip"):
        raise SystemExit("TimeCrystalFlipWaveProfile.asset: could not read the loop or the flip curves")
    return {"loop": head["loopSeconds"], "first": head["firstRingStartSeconds"], "stagger": head["ringStaggerSeconds"], "curves": curves}


def charge_and_space_fx():
    """The Charge crystal's discharge (ChargeCrystalMaterial.mat, read by ChargeCrystal.hlsl) and tumble
    (JustRotate on CrystalCharge.prefab), and the Space crystal's spin timing (SpaceCrystalAnimator on
    CrystalSpace.prefab)."""
    mat = read(os.path.join(MATS, "ChargeCrystalMaterial.mat"))
    f = {k: float(v) for k, v in re.findall(r"^\s+- (_\w+): (-?[0-9.eE+-]+)$", mat, re.M)}
    core = colour(mat, "_ArcCoreColor")
    arcs = {"speed": f["_ArcSpeed"], "length": f["_ArcLength"], "duty": f["_ArcDuty"], "intensity": f["_ArcIntensity"],
            "core": core}
    charge = read(os.path.join(PREFABS, "CrystalCharge.prefab"))
    m = re.search(r"  speed: (\S+)\n  direction: \{x: (\S+), y: (\S+), z: (\S+)\}", charge)
    if not m:
        raise SystemExit("CrystalCharge.prefab: no JustRotate speed/direction")
    tumble = {"speed": float(m.group(1)), "direction": [float(m.group(i)) for i in (2, 3, 4)]}
    space = read(os.path.join(PREFABS, "CrystalSpace.prefab"))
    cs = re.search(r"^  cycleSpeed: (\S+)$", space, re.M)
    ps = re.search(r"^  pauseAfterSpin: (\S+)$", space, re.M)
    if not cs or not ps:
        raise SystemExit("CrystalSpace.prefab: no SpaceCrystalAnimator cycleSpeed/pauseAfterSpin")
    return {"arcs": arcs, "chargeTumble": tumble, "spaceSpin": {"cycleSpeed": float(cs.group(1)), "pause": float(ps.group(1))}}


def build():
    d = json.loads(read(DATA))
    validate(d)
    d["colours"] = shipped_colours()
    d["omniFx"] = omni_effects(d)
    d["omniFx"]["flip"] = flip_profile()
    d["omniFx"].update(charge_and_space_fx())
    r5 = lambda a: [round(float(x), 5) for x in np.asarray(a).ravel()]
    for element in SITE_KINDS:
        stage1, final, twists, plate_twist, plate_axis = wedge_targets(d, element)
        c = next(x for x in d["crystals"] if x["element"] == element)
        c["stage1"], c["wedge"] = r5(stage1), r5(final)
        c["twistDeg"] = round(float(np.mean(twists)), 2)
        c["plateTwist"], c["plateAxis"] = plate_twist, plate_axis
    quats, final, residual, slips = time_rigid(d)
    c = next(x for x in d["crystals"] if x["element"] == "Time")
    c["rigidQuat"], c["rigid"], c["rigidResidual"], c["artSlips"] = quats, r5(final), round(residual, 4), slips
    d["data"] = "Docs/Studios/data/omni_crystal_combine.json (Tools/Build/bake_omni_crystal_combine.py)"
    return d


def summary(d):
    extra = lambda c: (f" twist {c['twistDeg']} deg" if "twistDeg" in c else "") + \
        (f" rigid {c['rigidResidual']}" if "rigidResidual" in c else "")
    return "; ".join(f"{c['element']} {len(c['basis']) // 3} verts x{c['stack']}{extra(c)}" for c in d["crystals"]) + \
        f"; omni {len(d['omni']['verts']) // 3} verts"


def splice(page, block, where=LAB):
    """Replace the text between the marker LINES (each alone on its line, exactly one of each)."""
    opens = [m for m in re.finditer(r"^[ \t]*" + re.escape(OPEN) + r"[ \t]*$", page, re.M)]
    closes = [m for m in re.finditer(r"^[ \t]*" + re.escape(CLOSE) + r"[ \t]*$", page, re.M)]
    if len(opens) != 1 or len(closes) != 1 or closes[0].start() < opens[0].end():
        raise SystemExit(f"{where}: expected exactly one '{OPEN}' line before one '{CLOSE}' line")
    return page[:opens[0].end()] + "\n" + block + "\n" + page[closes[0].start():]


def block_for(d):
    return "  const ASSETS = " + json.dumps(d, separators=(",", ":")) + ";"


def self_test():
    ok = True

    def check(name, cond):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name)
        ok = ok and cond
    page = "x\n  // names " + OPEN + " in prose\n  " + OPEN + "\n  old\n  " + CLOSE + "\ny\n"
    out = splice(page, "NEW")
    check("splice replaces only between the marker lines", "NEW" in out and "old" not in out and "in prose" in out)
    try:
        splice("  " + OPEN + "\n  " + OPEN + "\n  " + CLOSE + "\n", "x")
        check("two open markers are refused", False)
    except SystemExit:
        check("two open markers are refused", True)
    d = json.loads(read(DATA))
    validate(d)
    check("the shipped combine data validates", True)
    bad = json.loads(read(DATA))
    bad["crystals"] = [c for c in bad["crystals"] if c["element"] != "Time"]
    try:
        validate(bad)
        check("a combine missing a crystal is refused (the omni's rhombi go uncovered)", False)
    except SystemExit:
        check("a combine missing a crystal is refused (the omni's rhombi go uncovered)", True)
    bad = json.loads(read(DATA))
    bad["crystals"][0]["lands"] = bad["crystals"][0]["lands"][:-1]
    try:
        validate(bad)
        check("a crystal whose arrays disagree is refused", False)
    except SystemExit:
        check("a crystal whose arrays disagree is refused", True)
    cols = shipped_colours()
    check("every elemental crystal's material carries a colour pair", all(e["bright"] for e in cols["elements"].values()))
    wedge_targets(d, "Charge")
    wedge_targets(d, "Mass")
    time_rigid(d)
    check("the shipped Charge and Mass wedges tile their faces; Time locks in rigidly", True)
    bad = json.loads(read(DATA))
    c = next(x for x in bad["crystals"] if x["element"] == "Charge")
    oplate = bad["omni"]["plate"]
    by_plate = {}
    for i, p in enumerate(c["plate"]):
        by_plate.setdefault(p, []).append(i)
    site_of = {p: oplate[c["lands"][idx[0]]] for p, idx in by_plate.items()}
    a = next(iter(by_plate))
    b = next(p for p in by_plate if site_of[p] != site_of[a])
    for i, j in zip(by_plate[a], by_plate[b]):   # petal a now lands where petal b does: 4 at one site, 6 at another
        c["lands"][i] = c["lands"][j]
    try:
        wedge_targets(bad, "Charge")
        check("a Charge petal moved to another pentagon is refused", False)
    except SystemExit:
        check("a Charge petal moved to another pentagon is refused", True)
    bad = json.loads(read(DATA))
    c = next(x for x in bad["crystals"] if x["element"] == "Time")
    idx = [i for i, p in enumerate(c["plate"]) if p == 0]
    B = _arr(c["basis"])
    ctr = B[idx].mean(axis=0)
    for i in idx:   # a rhombus 30% too big cannot lock in without a morph
        c["basis"][3 * i:3 * i + 3] = [float(x) for x in ctr + 1.3 * (B[i] - ctr)]
    try:
        time_rigid(bad)
        check("a Time rhombus that would need a morph to land is refused", False)
    except SystemExit:
        check("a Time rhombus that would need a morph to land is refused", True)
    sys.exit(0 if ok else 1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        self_test()
    d = build()
    block, stale = block_for(d), []
    for lab in LABS:
        page = read(lab)
        new = splice(page, block, lab)
        if args.check:
            if new != page:
                stale.append(lab)
            continue
        with open(lab, "w") as f:
            f.write(new)
        print(f"wrote {lab}: {summary(d)}")
    if args.check:
        if stale:
            for lab in stale:
                print(f"STALE: {lab} does not carry the current combine data. {summary(d)}")
            sys.exit(1)
        print(f"OK: {len(LABS)} labs match the data. {summary(d)}")

if __name__ == "__main__":
    main()
