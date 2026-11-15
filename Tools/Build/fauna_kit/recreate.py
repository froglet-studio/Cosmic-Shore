#!/usr/bin/env python3
"""Recreate the fauna the game already has from the kit's specs, and MEASURE the difference.

    python3 Tools/Build/fauna_kit/recreate.py                 # the table: every creature, spec body against shipped body
    python3 Tools/Build/fauna_kit/recreate.py --check         # exit 1 when any difference is over the tolerance
    python3 Tools/Build/fauna_kit/recreate.py --write-specs   # (re)write specs/shipped/<key>.py from the dossier
    python3 Tools/Build/fauna_kit/recreate.py --self-test     # a planted drift in every measured quantity must fail
    python3 Tools/Build/fauna_kit/recreate.py --sheet <png>   # shipped (top) and recreated (bottom), drawn

Garrett, 2026-11-15: "try to get to a point where you can recreate the existing fauna like you did when you learned to
create better vessels. you recreated the best vessels we had until you couldn't measure a difference."

Two bodies are compared for each creature, built by two different paths:
- SHIPPED: dossier.measure reads the prefab, the model files and the config assets again, now.
- RECREATED: body.from_spec builds a body from the creature's spec file, the same function a new species goes through.
  The Bee's spec is the kit's GENERATIVE language (braid spindles from -> to, prisms by rule): its body is rebuilt from
  rules and held to the prefab the game ships. The other six are TRANSCRIPTIONS (specs/shipped/): the kit's language
  states their meshes by reference and their prisms, heart and attack limbs by pose.

What is measured, per creature: every prism (matched by name: position, size, facing, kind, shielded), every wire
(matched by name: the largest distance between corresponding vertices), the heart's seat, the attack limbs (joint by
joint), and the numbers a rider's possession reads (top speed, mouth reach). TOL is the reader's own rounding (5
decimals) with room: a difference above it is a real one.
"""
import copy
import os
import pprint
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import body as BODY  # noqa: E402
import celestial as C  # noqa: E402
import dossier as D  # noqa: E402

SHIPPED = os.path.join(HERE, "specs", "shipped")
TOL = 2e-3          # world units at root scale 1 (the bodies are 10-100 u; the readers round to 1e-5)
GENERATIVE = {"bee": "bee"}     # dossier key -> the celestial spec that rebuilds it from rules


# ------------------------------------------------------------------ the shipped specs (transcriptions)

def _part_spec(b):
    return {"root_scale": b["root_scale"],
            "skin": [{"name": w["name"], "mesh": w["source"], "limb": w["limb"], "local": w["local"]} for w in b["wires"]],
            "prisms": [{k: p[k] for k in ("name", "kind", "shielded", "at", "q", "size", "limb")} for p in b["prisms"]],
            "heart": {"at": b["heart"]["at"], "scale": b["heart"]["scale"]} if b["heart"] else None,
            "limbs": b["chains"], "spindles": b["spindles"]}


def spec_from_dossier(d):
    spec = {"species": d["name"], "shipped": True, "prefab": d["prefab"], "klass": d["klass"], "role": d["diet"],
            "possession": dict(d["possession"]), "numbers": dict(d["numbers"]),
            "variants": {e: {k: v[k] for k in ("body_scale", "min_speed", "max_speed", "erode_spindle")} for e, v in d["variants"].items()}}
    if "segments" in d:
        spec["segments"] = {role: dict(_part_spec(b), prefab=b["prefab"]) for role, b in d["segments"].items()}
    else:
        spec.update(_part_spec(d))
    return spec


def write_specs():
    os.makedirs(SHIPPED, exist_ok=True)
    for key in D.ROSTER:
        if key in GENERATIVE:
            continue
        spec = spec_from_dossier(D.measure(key))
        head = (f'"""{spec["species"]}: a SHIPPED creature, transcribed into the kit\'s spec language (recreate.py).\n\n'
                f'Measured from {spec["prefab"]} by dossier.py; `recreate.py --check` holds this file to the prefab, so a\n'
                f'change to either shows as a difference. It is not a celestial design: `celestial.py --shipped` lists the\n'
                f'laws this body breaks (the redesign list, /fauna-creation section 7.1). Wires are the shipped meshes by\n'
                f'reference (`mesh`, placed by `local`, a 3x4 matrix in the creature\'s space at root scale 1).\n"""\n')
        with open(os.path.join(SHIPPED, key + ".py"), "w") as f:
            f.write(head + "SPEC = " + pprint.pformat(spec, width=150, sort_dicts=False, compact=True) + "\n")
        print("wrote", os.path.relpath(os.path.join(SHIPPED, key + ".py"), D.REPO))


def dossier_keys():
    return [k for k in D.ROSTER if k not in GENERATIVE]


def load_spec(key):
    if key in GENERATIVE:
        return C.load(GENERATIVE[key])
    import importlib.util
    sp = importlib.util.spec_from_file_location("shipped_" + key, os.path.join(SHIPPED, key + ".py"))
    mod = importlib.util.module_from_spec(sp)
    sp.loader.exec_module(mod)
    return mod.SPEC


# ------------------------------------------------------------------ the measurement

def _dist(a, b):
    return sum((a[i] - b[i]) ** 2 for i in range(len(a))) ** 0.5


def _qdist(a, b):
    return min(_dist(a, b), _dist(a, [-v for v in b]))


def _box_dist(p, q):
    """The largest distance between two prisms' corresponding corners is not usable when a box is symmetric (a 180
    degree turn about an axis is the same box). So: centre, sorted size, and the three face axes up to sign."""
    import fbx_model as fm
    d = max(_dist(p["at"], q["at"]), _dist(sorted(p["size"]), sorted(q["size"])))
    ma, mb = fm.quat_to_m(p["q"]), fm.quat_to_m(q["q"])
    # the long axis (largest size) must point along the same line
    ia, ib = max(range(3), key=lambda i: p["size"][i]), max(range(3), key=lambda i: q["size"][i])
    la, lb = [ma[r][ia] for r in range(3)], [mb[r][ib] for r in range(3)]
    na, nb = sum(v * v for v in la) ** 0.5 or 1.0, sum(v * v for v in lb) ** 0.5 or 1.0
    align = abs(sum(la[i] * lb[i] for i in range(3))) / (na * nb)
    if len(set(round(v, 4) for v in p["size"])) == 3 or p["size"][ia] > 1.5 * sorted(p["size"])[1]:
        d = max(d, (1.0 - min(1.0, align)) * max(p["size"]))
    return d


def compare_bodies(shipped, made, generative):
    """{'prisms', 'wires', 'heart', 'limbs', 'notes'}: the largest difference of each kind, in world units."""
    out = {"prisms": 0.0, "wires": 0.0, "heart": 0.0, "limbs": 0.0, "notes": []}
    sp = {p["name"]: p for p in shipped["prisms"]}
    mp = {p["name"]: p for p in made["prisms"]}
    if set(sp) != set(mp):
        out["notes"].append(f"prism names differ: only shipped {sorted(set(sp) - set(mp))}, only recreated {sorted(set(mp) - set(sp))}")
        out["prisms"] = float("inf")
    for name in set(sp) & set(mp):
        a, b = sp[name], mp[name]
        if a["kind"] != b["kind"] or bool(a.get("shielded")) != bool(b.get("shielded")):
            out["notes"].append(f"prism {name}: kind {a['kind']}/{a.get('shielded')} against {b['kind']}/{b.get('shielded')}")
            out["prisms"] = float("inf")
        out["prisms"] = max(out["prisms"], _box_dist(a, b))
    # wires: a generative body names its braid after its spindle; the prefab names every braid 'Braid' under it
    sw = {(w["limb"] if generative else w["name"]): w for w in shipped["wires"]}
    mw = {w["name"]: w for w in made["wires"]}
    if set(sw) != set(mw):
        out["notes"].append(f"wire names differ: only shipped {sorted(set(sw) - set(mw))}, only recreated {sorted(set(mw) - set(sw))}")
        out["wires"] = float("inf")
    for name in set(sw) & set(mw):
        a, b = sw[name]["verts"], mw[name]["verts"]
        if len(a) != len(b):
            out["notes"].append(f"wire {name}: {len(a)} against {len(b)} vertices")
            out["wires"] = float("inf")
            continue
        out["wires"] = max(out["wires"], max(_dist(x, y) for x, y in zip(a, b)))
    if shipped.get("heart") and made.get("heart"):
        out["heart"] = _dist(shipped["heart"]["at"], made["heart"]["at"])
    elif bool(shipped.get("heart")) != bool(made.get("heart")):
        out["heart"] = float("inf")
    sl, ml = shipped["chains"], made["chains"]
    if len(sl) != len(ml):
        out["notes"].append(f"{len(sl)} attack limbs shipped, {len(ml)} recreated")
        out["limbs"] = float("inf")
    else:
        # limbs are matched whole (several share a root joint, and the order the hub lists them in is not a body
        # property): each shipped limb takes the recreated limb of the same joint count that lies closest, joint by joint
        def limb_dist(a, b):
            return max(_dist(x, y) for x, y in zip(a["at"], b["at"])) if len(a["at"]) == len(b["at"]) else float("inf")
        left = list(ml)
        for a in sl:
            b = min(left, key=lambda c: limb_dist(a, c))
            d = limb_dist(a, b)
            if d == float("inf"):
                out["notes"].append(f"limb {a['joints'][0]}: no recreated limb has its {len(a['at'])} joints")
            else:
                left.remove(b)
            out["limbs"] = max(out["limbs"], d)
    return out


def compare(key, spec=None, dossier=None):
    d = dossier or D.measure(key)
    spec = spec or load_spec(key)
    generative = key in GENERATIVE
    made = BODY.from_spec(spec)
    rows = []
    if "segments" in d:
        for role, sb in d["segments"].items():
            rows.append((f"{d['name']} {role}", compare_bodies(sb, made["segments"][role], generative)))
    else:
        rows.append((d["name"], compare_bodies(d, made, generative)))
    numbers = 0.0
    pos = spec.get("possession", {})
    for k in ("top_speed", "mouth_reach"):
        if k in pos:
            numbers = max(numbers, abs(pos[k] - d["possession"][k]))
        else:
            numbers = float("inf")
    return rows, numbers, ("rebuilt from rules" if generative else "transcribed")


def table(keys=None, specs=None):
    worst = 0.0
    lines = []
    for key in keys or D.ROSTER:
        rows, numbers, how = compare(key, spec=(specs or {}).get(key))
        for name, r in rows:
            w = max(r["prisms"], r["wires"], r["heart"], r["limbs"], numbers)
            worst = max(worst, w)
            lines.append((name, how, r, numbers, w))
    return lines, worst


def self_test():
    """Each measured quantity catches a planted drift; an untouched spec passes."""
    fails = []
    base_lines, base_worst = table()
    if base_worst > TOL:
        fails.append(f"the untouched specs already differ by {base_worst:.5f} (run --check)")

    def planted(label, key, mutate, column):
        spec = copy.deepcopy(load_spec(key))
        mutate(spec)
        rows, numbers, _ = compare(key, spec=spec)
        got = numbers if column == "numbers" else max(r[column] for _n, r in rows)
        if not got > TOL:
            fails.append(f"planted '{label}' was not measured ({column} = {got})")

    planted("a shark tooth moved 0.02", "shark", lambda s: s["prisms"][0]["at"].__setitem__(0, s["prisms"][0]["at"][0] + 0.02), "prisms")
    planted("a quadfish fin resized", "quadfish", lambda s: s["prisms"][0]["size"].__setitem__(1, s["prisms"][0]["size"][1] * 1.01), "prisms")
    planted("a shark tooth made health", "shark",
            lambda s: [p.update(kind="health") for p in s["prisms"] if p["kind"] == "danger"][:1], "prisms")
    planted("the clawfish model shifted 0.01", "clawfish", lambda s: s["skin"][0]["local"][2].__setitem__(3, s["skin"][0]["local"][2][3] + 0.01), "wires")
    planted("the brittlestar's heart moved", "brittlestar", lambda s: s["heart"]["at"].__setitem__(2, 0.05), "heart")
    planted("a brittlestar arm joint moved", "brittlestar", lambda s: s["limbs"][0]["at"][2].__setitem__(1, 0.5), "limbs")
    planted("the shark's top speed changed", "shark", lambda s: s["possession"].update(top_speed=36.0), "numbers")
    planted("a bee vane swept further back (the generative spec)", "bee",
            lambda s: [sp["to"].__setitem__(2, sp["to"][2] - 0.2) for sp in s["spindles"] if sp["name"] == "Vane0"], "wires")
    planted("a bee rib moved (the generative spec)", "bee",
            lambda s: [p["at"].__setitem__(2, -8.1) for p in s["prisms"] if p["name"] == "Rib0"], "prisms")
    return fails


def main(argv):
    if "--write-specs" in argv:
        write_specs()
        return 0
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("recreate self-test:", "ok (9 planted drifts measured)" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    lines, worst = table()
    print(f"{'creature':<18} {'how':<19} {'prisms':>9} {'wires':>9} {'heart':>9} {'limbs':>9} {'numbers':>9}   (largest difference, world units)")
    for name, how, r, numbers, _w in lines:
        print(f"{name:<18} {how:<19} {r['prisms']:>9.5f} {r['wires']:>9.5f} {r['heart']:>9.5f} {r['limbs']:>9.5f} {numbers:>9.5f}")
        for n in r["notes"]:
            print("    ", n)
    print(f"largest difference anywhere: {worst:.5f} u (tolerance {TOL})")
    if "--sheet" in argv:
        out = argv[argv.index("--sheet") + 1]
        pairs = []
        for key in D.ROSTER:
            d = D.measure(key)
            made = BODY.from_spec(load_spec(key))
            if "segments" in d:
                continue
            made["root_scale"] = d["root_scale"]
            pairs += [(d["name"] + " shipped", d), (d["name"] + " recreated", made)]
        print("sheet:", D.sheet(pairs, out, "shipped (measured from the prefab) against recreated (built from the spec)", size=360))
    if "--check" in argv and worst > TOL:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
