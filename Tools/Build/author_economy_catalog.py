#!/usr/bin/env python3
"""Writes the shop prices the server checks into Tools/UGS/CloudCode/economy.js (fair play, Threat Board T3).

    python3 Tools/Build/author_economy_catalog.py           # rewrite the CATALOG block in economy.js
    python3 Tools/Build/author_economy_catalog.py --check   # exit 1, naming the first difference, if it drifted
    python3 Tools/Build/author_economy_catalog.py --self-test

The server sells vessel unlocks and Hangar levels itself (economy.js `unlockVessel`, `upgradeHangar`), so it must know
the prices, and the prices are authored in Unity: each vessel's `UnlockCost` and `ownedFromStart` on its SO_Vessel,
the Hangar's `crystalsPerLevel` on HangarView.prefab, and `HangarUpgradeStore.MaxLevel`. Cloud Code cannot read
Unity assets, so this generator copies them between the `// <catalog>` markers. The assets stay the source: change a
price in Unity, run this, redeploy economy.js. `--check` diffs against disk, so a price changed without re-running
this fails the check instead of letting the server charge an old price.

Which vessels: every SO_Vessel any SO_Classlist_*.asset references (the lists the game ships), by `Name`. Two shipped
vessels with one Name and different prices is an error (an unshipped `_TEMP` copy is never read).
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ECONOMY = os.path.join(ROOT, "Tools", "UGS", "CloudCode", "economy.js")
LISTS = "Assets/_SO_Assets/Classes/SO_Classlist_*.asset"
HANGAR_PREFAB = "Assets/_Prefabs/UI Elements/Main Menu Screens/Hangar/HangarView.prefab"
STORE_CS = "Assets/_Scripts/UI/Elements/Hangar/HangarUpgradeStore.cs"
BEGIN, END = "// <catalog>", "// </catalog>"


def guid_index():
    idx = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets", "**", "*.asset.meta"), recursive=True):
        with open(meta, encoding="utf-8", errors="replace") as fh:
            m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
        if m:
            idx[m.group(1)] = meta[:-5]
    return idx


def field(text, name):
    m = re.search(r"^  " + re.escape(name) + r": ?(.*)$", text, re.M)
    return m.group(1).strip() if m else None


def read_vessels():
    idx = guid_index()
    vessels, where = {}, {}
    for lst in sorted(glob.glob(os.path.join(ROOT, LISTS))):
        with open(lst, encoding="utf-8") as fh:
            for g in re.findall(r"guid: ([0-9a-f]{32})", fh.read()):
                path = idx.get(g)
                if not path:
                    continue
                with open(path, encoding="utf-8", errors="replace") as vf:
                    text = vf.read()
                name = field(text, "Name")
                cost = field(text, "UnlockCost")
                if name is None or cost is None:
                    continue                       # not an SO_Vessel (the list's own script guid)
                entry = {"cost": int(cost), "starter": field(text, "ownedFromStart") == "1"}
                rel = os.path.relpath(path, ROOT)
                if name in vessels and vessels[name] != entry:
                    raise SystemExit(f"two shipped vessels named {name!r} disagree: {where[name]} {vessels[name]} vs {rel} {entry}")
                vessels[name], where[name] = entry, rel
    if not vessels:
        raise SystemExit(f"no vessels found from {LISTS}")
    return dict(sorted(vessels.items()))


def read_hangar():
    with open(os.path.join(ROOT, HANGAR_PREFAB), encoding="utf-8") as fh:
        per = re.findall(r"^  crystalsPerLevel: (\d+)", fh.read(), re.M)
    if len(per) != 1:
        raise SystemExit(f"expected one crystalsPerLevel in {HANGAR_PREFAB}, found {len(per)}")
    with open(os.path.join(ROOT, STORE_CS), encoding="utf-8") as fh:
        m = re.search(r"public const int MaxLevel = (\d+);", fh.read())
    if not m:
        raise SystemExit(f"HangarUpgradeStore.MaxLevel not found in {STORE_CS}")
    return {"perLevel": int(per[0]), "maxLevel": int(m.group(1))}


def render(vessels, hangar):
    lines = [BEGIN + " written by Tools/Build/author_economy_catalog.py from the shipped assets; do not edit by hand",
             "const CATALOG = {",
             "  vessels: {"]
    for name, v in vessels.items():
        lines.append(f"    {json.dumps(name)}: {{ cost: {v['cost']}, starter: {'true' if v['starter'] else 'false'} }},")
    lines += ["  },",
              f"  hangar: {{ perLevel: {hangar['perLevel']}, maxLevel: {hangar['maxLevel']} }},",
              "};",
              END]
    return "\n".join(lines)


def splice(text, block):
    i, j = text.find(BEGIN), text.find(END)
    if i < 0 or j < 0 or j < i:
        raise SystemExit(f"catalog markers not found in {ECONOMY}")
    return text[:i] + block + text[j + len(END):]


def self_test():
    sample = "a\n// <catalog> old\nconst CATALOG = {};\n// </catalog>\nb\n"
    out = splice(sample, render({"Squirrel": {"cost": 0, "starter": True}}, {"perLevel": 100, "maxLevel": 10}))
    bad = []
    if '"Squirrel": { cost: 0, starter: true }' not in out: bad.append("vessel row")
    if "hangar: { perLevel: 100, maxLevel: 10 }" not in out: bad.append("hangar row")
    if not out.startswith("a\n") or not out.endswith("\nb\n"): bad.append("text outside the markers changed")
    if "old" in out: bad.append("old block kept")
    print("self-test: " + ("ok" if not bad else "FAILED: " + ", ".join(bad)))
    return 1 if bad else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    with open(ECONOMY, encoding="utf-8") as fh:
        current = fh.read()
    wanted = splice(current, render(read_vessels(), read_hangar()))
    if "--check" in argv:
        if wanted == current:
            print("economy catalog: OK (matches the shipped assets)")
            return 0
        a, b = current.splitlines(), wanted.splitlines()
        for n, (x, y) in enumerate(zip(a, b), 1):
            if x != y:
                print(f"economy catalog: DRIFT in {os.path.relpath(ECONOMY, ROOT)} line {n}:\n  has    {x}\n  wanted {y}")
                break
        else:
            print(f"economy catalog: DRIFT in {os.path.relpath(ECONOMY, ROOT)} (length differs)")
        print("run python3 Tools/Build/author_economy_catalog.py, then redeploy economy.js")
        return 1
    if wanted != current:
        with open(ECONOMY, "w", encoding="utf-8") as fh:
            fh.write(wanted)
        print("economy catalog: written")
    else:
        print("economy catalog: already current")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
