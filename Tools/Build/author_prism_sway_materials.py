#!/usr/bin/env python3
"""
Add the four `_Sway*` defaults to every material that draws with a prism graph.

Docs/ECOSYSTEM.md §47. `wire_prism_sway.py` adds the properties to BlockGraph and
ExplodingBlockGraph; Unity would write these rows into each `.mat` itself on the next
import. Writing them here keeps the repo self-consistent — the `_Jiggle*` rows are
already serialized in these files — and, more usefully, puts the DEFAULTS in the diff,
so a reviewer can see that every shipped material is the exact no-op and that nothing
but a living limb's own prisms will move.

The materials are found by SHADER GUID rather than by name, so a material renamed or
added since is picked up and one that merely mentions a prism property is not.

Usage:  python3 Tools/Build/author_prism_sway_materials.py [--check]
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MATERIALS = os.path.join(REPO, "Assets/_Graphics/Materials")

GRAPH_METAS = [
    "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph.meta",
    "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph.meta",
]

# Vector3 shader properties serialize under m_Colors as {r,g,b,a}; the defaults are the
# exact no-op PrismSway_float is written around.
ROWS = [
    ("_SwayAxis", "{r: 0, g: 0, b: 0, a: 0}"),
    ("_SwaySpanX", "{r: 0, g: 0, b: 0, a: 0}"),
    ("_SwaySpanY", "{r: 0, g: 0, b: 0, a: 0}"),
    ("_SwayTiming", "{r: 0, g: 0, b: 0, a: 0}"),
]


def graph_guids():
    out = []
    for rel in GRAPH_METAS:
        with open(os.path.join(REPO, rel)) as fh:
            m = re.search(r"^guid: (\w+)$", fh.read(), re.M)
        assert m, rel + " has no guid"
        out.append(m.group(1))
    return out


def targets():
    guids = graph_guids()
    found = []
    for root, _dirs, files in os.walk(os.path.join(REPO, "Assets")):
        for name in files:
            if not name.endswith(".mat"):
                continue
            path = os.path.join(root, name)
            with open(path, encoding="utf-8", errors="replace") as fh:
                text = fh.read()
            m = re.search(r"m_Shader: \{fileID: -?\d+, guid: (\w+)", text)
            if m and m.group(1) in guids:
                found.append((path, text))
    return sorted(found)


def insert(text):
    """Insert each row into m_Colors in ALPHABETICAL order — Unity writes that list
    sorted, so appending would produce a file Unity immediately rewrites."""
    lines = text.split("\n")
    try:
        start = next(i for i, l in enumerate(lines) if l.strip() == "m_Colors:")
    except StopIteration:
        return None
    end = start + 1
    while end < len(lines) and re.match(r"\s+- _\w+: \{", lines[end]):
        end += 1
    block = lines[start + 1:end]
    indent = re.match(r"(\s*)", block[0]).group(1) if block else "    "
    have = {re.match(r"\s+- (_\w+):", l).group(1) for l in block}
    added = 0
    for key, value in ROWS:
        if key in have:
            continue
        row = f"{indent}- {key}: {value}"
        pos = 0
        while pos < len(block) and re.match(r"\s+- (_\w+):", block[pos]).group(1) < key:
            pos += 1
        block.insert(pos, row)
        added += 1
    if added == 0:
        return None
    return "\n".join(lines[:start + 1] + block + lines[end:])


def main():
    check = "--check" in sys.argv
    found = targets()
    assert found, "no material draws with a prism graph — the guid lookup is wrong"
    stale = []
    for path, text in found:
        out = insert(text)
        if out is None:
            continue
        stale.append(path)
        if not check:
            with open(path, "w", encoding="utf-8") as fh:
                fh.write(out)
    rel = [os.path.relpath(p, REPO) for p in stale]
    if check:
        if stale:
            print(f"FAIL: {len(stale)} of {len(found)} prism materials are missing _Sway* rows:")
            for r in rel:
                print("  *", r)
            return 1
        print(f"OK: all {len(found)} prism materials carry the _Sway* defaults")
        return 0
    for r in rel:
        print("  wrote", r)
    print(f"{len(stale)} of {len(found)} materials updated")
    return 0


if __name__ == "__main__":
    sys.exit(main())
