#!/usr/bin/env python3
"""
Bakes the Omni Combine Lab's data block (Docs/Studios/OmniCombineLab.html, between the lines `// <assets>`
and `// </assets>`):

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

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LAB = os.path.join(ROOT, "Docs", "Studios", "OmniCombineLab.html")
DATA = os.path.join(ROOT, "Docs", "Studios", "data", "omni_crystal_combine.json")
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


def build():
    d = json.loads(read(DATA))
    validate(d)
    d["colours"] = shipped_colours()
    d["data"] = "Docs/Studios/data/omni_crystal_combine.json (Tools/Build/bake_omni_crystal_combine.py)"
    return d


def summary(d):
    return "; ".join(f"{c['element']} {len(c['basis']) // 3} verts x{c['stack']}" for c in d["crystals"]) + \
        f"; omni {len(d['omni']['verts']) // 3} verts"


def splice(page, block):
    """Replace the text between the marker LINES (each alone on its line, exactly one of each)."""
    opens = [m for m in re.finditer(r"^[ \t]*" + re.escape(OPEN) + r"[ \t]*$", page, re.M)]
    closes = [m for m in re.finditer(r"^[ \t]*" + re.escape(CLOSE) + r"[ \t]*$", page, re.M)]
    if len(opens) != 1 or len(closes) != 1 or closes[0].start() < opens[0].end():
        raise SystemExit(f"{LAB}: expected exactly one '{OPEN}' line before one '{CLOSE}' line")
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
    sys.exit(0 if ok else 1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        self_test()
    d = build()
    page = read(LAB)
    new = splice(page, block_for(d))
    if args.check:
        if new != page:
            print(f"STALE: {LAB} does not carry the current combine data. {summary(d)}")
            sys.exit(1)
        print(f"OK: lab matches the data. {summary(d)}")
        return
    with open(LAB, "w") as f:
        f.write(new)
    print(f"wrote {LAB}: {summary(d)}")


if __name__ == "__main__":
    main()
