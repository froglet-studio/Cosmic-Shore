#!/usr/bin/env python3
"""
Authors the OMNI COMBINE data the game plays (the Elemental Converter on the Ark screen):

    Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json   (a TextAsset, JsonUtility schema)

It is the Omni Combine Lab's own data block (`omni_combine_lab_assets.build()`: the art file's Basis /
Key 1 per crystal, the designer's derived targets - Charge's suck-in stage and wedges, Mass's wedges,
Time's rigid lock-in - and the effect numbers read from the shipped assets), re-expressed in UNITY'S axes
and in a schema `JsonUtility` can read. Nothing here is hand-placed; change the lab's derivation and re-run.

Unity's axes. Blender is right-handed Z-up; Unity's FBX import maps (x, y, z) -> (-x, z, -y), a
REFLECTION, which is what keeps a model looking the same in a left-handed engine. So:
  - positions:            p' = M p
  - rotation axes:        a' = -M a        (a rotation conjugated by a reflection: R(a, t) -> R(-M a, t))
  - quaternion vectors:   v' = -M v, w unchanged
The Charge crystal is chiral (the petals twist -13.1 deg into place), so a mirrored import would morph
the wrong way round: both rotations are re-proved in the new frame before anything is written (the
twist by fitting the best rotation about each site axis, the Time lock-in by landing every corner).

    python3 Tools/Build/author_omni_combine_asset.py            # write the asset
    python3 Tools/Build/author_omni_combine_asset.py --check    # exit 1 if the asset is stale
    python3 Tools/Build/author_omni_combine_asset.py --self-test
    python3 Tools/Build/author_omni_combine_asset.py --frame lab --out <path>   # the lab's own axes, for parity tests
"""

import argparse
import hashlib
import json
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import omni_combine_lab_assets as lab  # noqa: E402

ROOT = lab.ROOT
FOLDER = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "OmniCombine")
OUT = os.path.join(FOLDER, "OmniCombineData.json")
ORDER = ["Space", "Charge", "Mass", "Time"]          # the designer's order (2026-10-13)
# Blender -> Unity (FBX import) is a reflection; Blender -> the lab's WebGL frame is a rotation.
FRAMES = {
    "unity": np.array([[-1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=float),
    "lab": np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=float),
}
TWIST_FIT = 8.0       # deg: the best rotation about a site axis vs the baked twist (Charge 0.02, Mass 5.5)
RIGID_FIT = 1e-4      # the Time lock-in replayed in the new frame vs its baked rigid pose


def guid(name):
    return hashlib.md5(f"CosmicShore/OmniCombine/{name}".encode()).hexdigest()


def r5(a):
    return [round(float(x), 5) for x in np.asarray(a, dtype=float).ravel()]


def pts(flat):
    return np.asarray(flat, dtype=float).reshape(-1, 3)


def axis_map(M):
    """How a rotation AXIS maps: det(M) * M (a pseudovector)."""
    return np.linalg.det(M) * M


def quat_rot(q, v):
    w, u = q[0], np.asarray(q[1:])
    t = 2 * np.cross(u, v)
    return v + w * t + np.cross(u, t)


def best_twist(P, F, a):
    """Degrees of the rotation about `a` that best carries P's shape onto F's (both centred, projected)."""
    a = a / np.linalg.norm(a)
    P = P - P.mean(axis=0)
    F = F - F.mean(axis=0)
    P = P - np.outer(P @ a, a)
    F = F - np.outer(F @ a, a)
    s = sum(a @ np.cross(p, f) for p, f in zip(P, F))
    c = sum(p @ f for p, f in zip(P, F))
    return float(np.degrees(np.arctan2(s, c)))


def convert(d, M, axis_m=None):
    """The lab's data in frame M, in the JsonUtility schema. `axis_m` overrides the axis map (self-test)."""
    A = axis_map(M) if axis_m is None else axis_m
    P = lambda flat: r5(pts(flat) @ M.T)
    by = {c["element"]: c for c in d["crystals"]}
    crystals = []
    for element in ORDER:
        c = by[element]
        first = {}
        for i, q in enumerate(c["plate"]):
            first.setdefault(q, i)
        site = [d["omni"]["plate"][c["lands"][first[q]]] for q in range(len(first))]
        out = {"element": element, "basis": P(c["basis"]), "tris": c["tris"], "plate": c["plate"],
               "plateSite": site, "offset": r5(M @ np.asarray(c["offset"], dtype=float))}
        if "wedge" in c:
            out["final"], out["stage1"] = P(c["wedge"]), P(c["stage1"])
            out["plateAxis"] = r5(np.asarray(c["plateAxis"], dtype=float) @ A.T)
            out["plateTwist"] = [float(x) for x in c["plateTwist"]]
        elif "rigid" in c:
            out["final"] = P(c["rigid"])
            q = np.asarray(c["rigidQuat"], dtype=float)
            out["plateQuat"] = r5(np.column_stack([q[:, 0], q[:, 1:] @ A.T]))
        else:
            out["final"] = P(c["key1"])
        if "idle" in c:
            idle = c["idle"]
            out["idle"] = {"fps": float(idle["fps"]), "names": [k["name"] for k in idle["keys"]],
                           "keys": [{"delta": P(k["delta"])} for k in idle["keys"]],
                           "weights": [float(w) for row in idle["weights"] for w in row]}
        crystals.append(out)
    fx = d["omniFx"]
    curve = lambda keys: [float(x) for k in keys for x in k]
    flip = fx["flip"]
    sets = {ring: {name: curve(flip["curves"][ring].get(name, [])) for name in ("flip", "scale", "dip", "slide")}
            for ring in ("leadRing", "laterRings")}
    owner = {e: i for i, e in enumerate(ORDER)}
    sq = fx["squares"]
    return {
        "source": "Tools/Build/author_omni_combine_asset.py from " + d["data"],
        "order": ORDER,
        "omni": {"verts": P(d["omni"]["verts"]), "tris": d["omni"]["tris"], "plate": d["omni"]["plate"],
                 "plateOwner": [owner[e] for e in fx["plateOwner"]]},
        "crystals": crystals,
        "squares": {"fps": float(sq["fps"]), "keys": [{"verts": k["verts"], "delta": P(k["delta"])} for k in sq["keys"]],
                    "weights": [float(w) for row in sq["weights"] for w in row]},
        "depth": {"open": fx["depth"]["open"], "seal": fx["depth"]["seal"], "power": fx["depth"]["power"],
                  "omniReach": fx["depth"]["omni"]["reach"], "omniSizeStart": fx["depth"]["omni"]["sizeStart"],
                  "massReach": fx["depth"]["mass"]["reach"], "massSizeStart": fx["depth"]["mass"]["sizeStart"]},
        "flip": {"loop": flip["loop"], "first": flip["first"], "stagger": flip["stagger"],
                 "lead": sets["leadRing"], "later": sets["laterRings"]},
        "arcs": {"speed": fx["arcs"]["speed"], "length": fx["arcs"]["length"], "duty": fx["arcs"]["duty"]},
        "tumble": {"speed": fx["chargeTumble"]["speed"], "direction": [float(x) for x in fx["chargeTumble"]["direction"]]},
        "spin": {"cycleSpeed": fx["spaceSpin"]["cycleSpeed"], "pause": fx["spaceSpin"]["pause"]},
    }


def validate(out):
    """Re-prove both rotations in the written frame; raise SystemExit on a mismatch."""
    nv = len(out["omni"]["verts"]) // 3
    for c in out["crystals"]:
        n = len(c["basis"]) // 3
        if len(c["final"]) != 3 * n or len(c["plate"]) != n or max(c["tris"]) >= n:
            raise SystemExit(f"{c['element']}: arrays disagree on the vertex count")
        plates = {}
        for i, p in enumerate(c["plate"]):
            plates.setdefault(p, []).append(i)
        B, F = pts(c["basis"]), pts(c["final"])
        if "plateAxis" in c:
            S, Ax = pts(c["stage1"]), pts(c["plateAxis"])
            worst = max(abs(best_twist(S[idx], F[idx], Ax[p]) - c["plateTwist"][p]) for p, idx in plates.items())
            if worst > TWIST_FIT:
                raise SystemExit(f"{c['element']}: the twist runs the wrong way round in this frame "
                                 f"(best fit {worst:.1f} deg off the baked twist)")
        if "plateQuat" in c:
            Q = np.asarray(c["plateQuat"], dtype=float).reshape(-1, 4)
            worst = 0.0
            for p, idx in plates.items():
                cb, cf = B[idx].mean(axis=0), F[idx].mean(axis=0)
                for i in idx:
                    worst = max(worst, float(np.linalg.norm(cf + quat_rot(Q[p], B[i] - cb) - F[i])))
            if worst > RIGID_FIT:
                raise SystemExit(f"{c['element']}: the rigid lock-in misses its pose by {worst:.2e} in this frame")
    if len(out["omni"]["plateOwner"]) != max(out["omni"]["plate"]) + 1 or max(out["omni"]["tris"]) >= nv:
        raise SystemExit("omni: plate owners or triangles disagree with its vertices")


def render(out):
    """JSON with every number in plain decimal: values rounded to 5 places print as `1e-05`, and the
    exponent form is spelled out rather than left to JsonUtility's parser."""
    text = json.dumps(out, separators=(",", ":"))
    text = re.sub(r"(?<![\w.])(-?)(\d)e-05(?![\d.])", r"\g<1>0.0000\2", text)
    if re.search(r"\de[-+]?\d", text):
        raise SystemExit("an exponent survived in the combine data: " + re.search(r".{20}\de[-+]?\d.{5}", text).group(0))
    return text + "\n"


def meta_json(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


def meta_folder(g):
    return f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def files(frame="unity"):
    out = convert(lab.build(), FRAMES[frame])
    validate(out)
    return {OUT: render(out), OUT + ".meta": meta_json(guid("OmniCombineData.json")),
            FOLDER + ".meta": meta_folder(guid("folder"))}, out


def self_test():
    ok = True

    def check(name, cond):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name)
        ok = ok and cond

    def refused(fn):
        try:
            fn()
            return False
        except SystemExit:
            return True
    d = lab.build()
    M = FRAMES["unity"]
    check("Unity's import is a reflection, the lab's a rotation",
          round(np.linalg.det(M)) == -1 and round(np.linalg.det(FRAMES["lab"])) == 1)
    out = convert(d, M)
    validate(out)
    check("the Unity-frame data validates (twist and lock-in re-proved)", True)
    validate(convert(d, FRAMES["lab"]))
    check("the lab-frame data validates", True)
    check("a polar (unflipped) axis map is refused: Charge would twist the wrong way",
          refused(lambda: validate(convert(d, M, axis_m=M))))
    check("the designer's order is Space, Charge, Mass, Time", [c["element"] for c in out["crystals"]] == ORDER)
    check("every omni plate has an owning element", all(0 <= o < 4 for o in out["omni"]["plateOwner"]))
    bad = convert(d, M)
    bad["crystals"][3]["final"] = bad["crystals"][3]["final"][:-3]
    check("a crystal whose arrays disagree is refused", refused(lambda: validate(bad)))
    g = [guid("OmniCombineData.json"), guid("folder")]
    owners = 0
    for base, _, names in os.walk(os.path.join(ROOT, "Assets")):
        for n in names:
            path = os.path.join(base, n)
            if n.endswith(".meta") and path not in (OUT + ".meta", FOLDER + ".meta"):
                with open(path, encoding="utf-8", errors="replace") as f:
                    head = f.read(200)
                owners += sum(f"guid: {x}" in head for x in g)
    check("the asset guids are owned by no other .meta", owners == 0)
    sys.exit(0 if ok else 1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--frame", choices=sorted(FRAMES), default="unity")
    ap.add_argument("--out", help="write only the JSON, here (with --frame lab, for parity tests)")
    a = ap.parse_args()
    if a.self_test:
        self_test()
    written, out = files(a.frame)
    if a.out:
        with open(a.out, "w") as f:
            f.write(written[OUT])
        print(f"wrote {a.out} ({a.frame} frame)")
        return
    if a.frame != "unity":
        raise SystemExit("--frame lab needs --out (the shipped asset is always Unity's frame)")
    stale = [p for p, text in written.items() if not os.path.exists(p) or open(p, encoding="utf-8").read() != text]
    if a.check:
        if stale:
            print("STALE: " + ", ".join(os.path.relpath(p, ROOT) for p in stale))
            sys.exit(1)
        print(f"OK: {os.path.relpath(OUT, ROOT)} matches the lab's data")
        return
    os.makedirs(FOLDER, exist_ok=True)
    for p in stale:
        with open(p, "w", encoding="utf-8") as f:
            f.write(written[p])
    print(f"wrote {len(stale)} file(s); " + ", ".join(f"{c['element']} {len(c['basis']) // 3}" for c in out["crystals"]) +
          f", omni {len(out['omni']['verts']) // 3} verts")


if __name__ == "__main__":
    main()
