#!/usr/bin/env python3
"""Write the Fauna Possession Lab's data block from the kit: every shipped creature and every new spec.

    python3 Tools/Build/fauna_kit/lab_export.py            # rewrite the data block in Docs/Studios/FaunaLab.html
    python3 Tools/Build/fauna_kit/lab_export.py --check    # exit 1 when the page's block is not what the kit exports now

The lab is a READER (/labmaker 2.1): its creatures are not typed into the page. Each body is body.from_spec of the
creature's spec (the recreation recreate.py holds to the shipped prefab; a new species' celestial spec), packed as
quantised vertices and wire edges; each number is the dossier's (speeds, mouth reach, per-element body scale) or the
spec's possession block; and the Jewel Wasp's own numbers are read from JewelWaspConfig.asset. So when an asset or a
spec moves, re-running this moves the page, and --check says when it has not been run.
"""
import base64
import json
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import body as BODY  # noqa: E402
import celestial as C  # noqa: E402
import dossier as D  # noqa: E402
import recreate as R  # noqa: E402

PAGE = os.path.join(D.REPO, "Docs", "Studios", "FaunaLab.html")
BEGIN, END = "/*FAUNA_DATA_BEGIN*/", "/*FAUNA_DATA_END*/"
NEW_SPECIES = ("marlin", "basker", "medusa", "nautilus", "teredo")
WASP_CONFIG = "Assets/_SO_Assets/VesselActions/JewelWasp/JewelWaspConfig.asset"
Q = 0.002   # vertex quantum (u at root scale 1): int16 covers +-65 u, every body here is inside +-60


def pack(body):
    """A body for the page: one vertex buffer (int16, base64), wire edges (uint16 pairs), prisms, heart, limbs."""
    verts, edges = [], []
    for w in body["wires"]:
        base = len(verts)
        verts += w["verts"]
        seen = set()
        for poly in BODY.polys(w["source"]):
            for i in range(len(poly)):
                a, b = poly[i], poly[(i + 1) % len(poly)]
                seen.add((min(a, b), max(a, b)))
        edges += [(base + a, base + b) for a, b in sorted(seen)]
    hi = max((abs(c) for v in verts for c in v), default=0.0)
    q = Q if hi / Q < 32000 else hi / 32000
    vb = struct.pack("<%dh" % (len(verts) * 3), *[int(round(c / q)) for v in verts for c in v])
    fmt = "H" if len(verts) < 65536 else "I"
    eb = struct.pack("<%d%s" % (len(edges) * 2, fmt), *[i for e in edges for i in e])
    return {"q": q, "nv": len(verts), "v": base64.b64encode(vb).decode(), "ne": len(edges), "e32": fmt == "I",
            "e": base64.b64encode(eb).decode(),
            "prisms": [{"n": p["name"], "k": p["kind"], "sh": bool(p.get("shielded")), "at": p["at"], "q": p["q"], "s": p["size"],
                        "st": p.get("role") == "stinger"} for p in body["prisms"]],
            "heart": body["heart"]["at"] if body.get("heart") else [0, 0, 0],
            "limbs": [c["at"] for c in body["chains"]]}


def _extent(body):
    lo, hi = D.extent(body)
    return {"lo": [round(v, 2) for v in lo], "hi": [round(v, 2) for v in hi]}


def shipped_creature(key):
    d = D.measure(key)
    spec = R.load_spec(key)
    made = BODY.from_spec(spec)
    out = {"id": key, "name": d["name"], "origin": "shipped", "klass": d["klass"], "role": d["diet"], "prefab": d["prefab"],
           "top_speed": d["possession"]["top_speed"], "mouth_reach": d["possession"]["mouth_reach"], "numbers": d["numbers"]["asset"]}
    variants = {}
    for e, v in d["variants"].items():
        variants[e] = {"scale": v["body_scale"] if v["body_scale"] > 0 else None,
                       "max_speed": v["max_speed"] if v["max_speed"] is not None and v["max_speed"] >= 0 else None}
    out["variants"] = variants
    if "segments" in d:
        n = d["numbers"]
        out["worm"] = {k: n[k] for k in ("kaiju_scale", "segment_spacing", "taper_per_segment", "head_gap_multiplier", "tail_gap_multiplier",
                                         "spawn_segment_count", "follow_sharpness", "turn_degrees_per_second", "pursuit_turn_multiplier",
                                         "lunge_speed", "lunge_max_seconds", "undulation_yaw_degrees", "undulation_frequency",
                                         "undulation_phase_step")}
        out["segments"] = {role: dict(pack(b), ext=_extent(b)) for role, b in made["segments"].items()}
        out["root_scale"] = n["kaiju_scale"]
        out["attack"] = "charge"          # WormFauna.PlayPossessionAttack: its own lunge
        out["charge"] = {"speed": n["lunge_speed"], "seconds": n["lunge_max_seconds"]}
    else:
        out["root_scale"] = d["root_scale"]
        out["body"] = dict(pack(made), ext=_extent(made))
        out["attack"] = "whip" if made["chains"] else "lunge"   # FaunaAttackDriver.Scan on the body
    return out


def new_creature(key):
    spec = C.load(key)
    errs = C.check(spec)
    if errs:
        raise SystemExit(f"{key} breaks the celestial laws: {errs[0]}")
    made = BODY.from_spec(spec)
    pos = spec["possession"]
    out = {"id": key, "name": spec["species"], "origin": "new", "klass": "(proposal)", "role": spec["role"], "premise": spec["premise"],
           "spec": f"Tools/Build/fauna_kit/specs/{key}.py", "root_scale": 1.0, "symmetry": spec["symmetry"],
           "top_speed": pos["top_speed"], "mouth_reach": pos["mouth_reach"], "attack": pos["attack"],
           "body": dict(pack(made), ext=_extent(made)),
           "variants": {e: {"scale": v["body_scale"], "max_speed": pos["top_speed"] * v.get("speed_scale", 1.0) if "speed_scale" in v else None}
                        for e, v in spec["elements"].items()}}
    for k in ("turn_rate", "suction", "motion", "charge", "latch", "body_radius", "aim_cone", "for", "feel"):
        if k in pos:
            out[k] = pos[k]
    return out


def wasp_numbers():
    """JewelWaspConfig.asset's fields the lab reads, as {key: (value, 'JewelWaspConfigSO.field')}."""
    t = open(os.path.join(D.REPO, WASP_CONFIG)).read()

    def f(name):
        return float(re.search(r"^  %s: ([-\d.eE]+)" % name, t, re.M).group(1))

    def ef(name, key):
        block = re.search(r"^  %s:\n((?:    .*\n)+)" % name, t, re.M).group(1)
        return float(re.search(r"^    %s: ([-\d.eE]+)" % key, block, re.M).group(1))
    plain = ["aimConeDegrees", "stingSpeed", "stingSeconds", "stingCooldown", "hullScale", "nestedScale", "hostTurnDegrees",
             "hostRollDegrees", "hostSlowShare", "burstMultiplier", "burstSeconds", "burstCooldown", "suctionBites",
             "suctionInterval", "attackSeconds", "attackReach", "attackAimReaches", "leaveSpeed", "burstRadius",
             "hostCameraBodies", "hostCameraBase", "cameraEase"]
    out = {k: f(k) for k in plain}
    for name in ("hostSpeedScale", "suctionReachScale", "attackCooldown"):
        out[name + "Min"] = ef(name, "Min")
        out[name + "Max"] = ef(name, "Max")
    return out


def export():
    creatures = [shipped_creature(k) for k in D.ROSTER] + [new_creature(k) for k in NEW_SPECIES]
    lines, worst = R.table()
    return {"creatures": creatures, "wasp": wasp_numbers(), "waspAsset": WASP_CONFIG,
            "recreate": {"worst": round(worst, 6), "tol": R.TOL,
                         "rows": [{"name": n, "how": how, "prisms": round(r["prisms"], 6), "wires": round(r["wires"], 6),
                                   "heart": round(r["heart"], 6), "limbs": round(r["limbs"], 6), "numbers": round(num, 6)}
                                  for n, how, r, num, _w in lines]},
            "laws": {k: C.check_shipped(R.load_spec(k)) for k in R.dossier_keys()}}


def block():
    return BEGIN + "\nconst DATA = " + json.dumps(export(), separators=(",", ":")) + ";\n" + END


def main(argv):
    page = open(PAGE).read()
    a, b = page.index(BEGIN), page.index(END) + len(END)
    new = block()
    if "--check" in argv:
        ok = page[a:b] == new
        print("FaunaLab.html data block:", "current" if ok else "STALE (run lab_export.py)")
        return 0 if ok else 1
    open(PAGE, "w").write(page[:a] + new + page[b:])
    print(f"wrote the data block: {len(new) // 1024} KB, {len(json.loads(new[len(BEGIN) + 14:-len(END) - 2])['creatures'])} creatures")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
