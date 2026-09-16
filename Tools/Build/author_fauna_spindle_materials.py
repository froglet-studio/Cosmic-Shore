#!/usr/bin/env python3
"""
Move every FAUNA spindle onto FaunaSpindleGraph, and leave every FLORA spindle where it is.

SpindleMaterial is worn by twelve prefabs and only SIX of them are creatures: the shark, the
brittlestar, the three worm segments and the tadpole. The rest are flora branches, plus
CapsuleMembrane (not a lifeform at all) and two material VARIANTS of it
(Blue/FireProjectileMaterial, which inherit through m_Parent and must keep inheriting).
"A creature breathes, flows and rim-lights" is a statement about creatures, so the split is
per-material and the flora side comes out byte-for-byte unchanged.

What this owns
--------------
  * Assets/_Graphics/Materials/FaunaSpindleMaterial.mat      (new; SpindleMaterial's twin)
  * Assets/_Graphics/Materials/QuadFishSpindleMaterial.mat   (re-pointed in place)
  * the 19 material references across the six fauna prefabs

The QuadFish keeps its OWN material rather than joining the shared one, because it keeps its
own SWAY: amplitude transfers across meshes and FREQUENCY does not (CLAUDE.md §44), so a
shark at 1.4 rad/s and a small fish at 5.2 get different materials rather than a compromise.
Everything else about the two is identical, which is the point — flow, breath and rim are
properties of BEING A CREATURE.

The three new dials, and where their numbers come from
-----------------------------------------------------
  _RimStrength 0.45   additive, so it can only brighten; ~0.5 of the neutral rim colour at
                      grazing incidence, which reads as a lit silhouette without washing the
                      lacy interior out.
  _Pulse (0.8, 1.2)   +-20% around 1. CreatureTextureGraph ran 0.5..1.2, but it was
                      multiplying a TEXTURE; here it multiplies an already alpha-clipped
                      Voronoi, and a creature that dims to half reads as dying rather than
                      as breathing.
  _FlowSpeed (0,-0.01)  the Clawfish's OWN authored _Direction, carried over verbatim. At the
                      shipped CellDensity that walks one Voronoi cell in about a minute.

Usage:  python3 Tools/Build/author_fauna_spindle_materials.py [--check]
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

SPINDLE_MAT = "Assets/_Graphics/Materials/SpindleMaterial.mat"
FAUNA_MAT = "Assets/_Graphics/Materials/FaunaSpindleMaterial.mat"
QUADFISH_MAT = "Assets/_Graphics/Materials/QuadFishSpindleMaterial.mat"

SPINDLE_GRAPH_GUID = "545a7b2ee58b90744bdb1c9e265a09f4"
FAUNA_GRAPH_GUID = "3f7c1a9e5b2d4c08a6e1d7b4c9f30215"
SHADERGRAPH_SUBASSET = "-6465566751694194690"

SPINDLE_MAT_GUID = "4f44fa5c7514a2c45b5af7f45bc51acd"
FAUNA_MAT_GUID = "7b2e4f1a9c6d40518e3a5c7d2b8f4160"   # pinned: re-minting orphans every referrer

# Every fauna prefab that wears SpindleMaterial today, with the count this must move.
# A count that no longer matches means the prefab changed under us — stop, do not guess.
FAUNA_REFERRERS = {
    "Assets/_Models/Fauna/MassSharkFauna.prefab": 3,
    "Assets/_Models/Fauna/MassBrittlestarFauna.prefab": 11,
    "Assets/_Prefabs/FloraAndFauna/WormHeadSegment.prefab": 1,
    "Assets/_Prefabs/FloraAndFauna/WormBodySegment.prefab": 1,
    "Assets/_Prefabs/FloraAndFauna/WormTailSegment.prefab": 2,
    "Assets/_Prefabs/FloraAndFauna/Spindles/TadpoleSpindle.prefab": 1,
}

# Anything else that references SpindleMaterial and MUST NOT move.
FLORA_AND_OTHER = {
    "Assets/_Prefabs/FloraAndFauna/Spindles/AssemblyBranch.prefab",
    "Assets/_Prefabs/FloraAndFauna/Spindles/Branch.prefab",
    "Assets/_Prefabs/FloraAndFauna/Spindles/GyroidBranch.prefab",
    "Assets/_Prefabs/FloraAndFauna/Spindles/QuasicrystalBranch.prefab",
    "Assets/_Prefabs/Environment/CapsuleMembrane.prefab",
    "Assets/_Graphics/Materials/BlueProjectileMaterial.mat",
    "Assets/_Graphics/Materials/FireProjectileMaterial.mat",
}

NEW_FLOATS = {"_RimStrength": "0.45"}
NEW_COLORS = {
    "_Pulse": "{r: 0.8, g: 1.2, b: 0, a: 0}",
    "_FlowSpeed": "{r: 0, g: -0.01, b: 0, a: 0}",
}
# Painted from the palette at runtime now (FaunaNeutralPalette), or never declared at all.
DROP_COLORS = ("_BrightColor", "_DullColor", "_Color1", "_Color2")


def path(rel):
    return os.path.join(REPO, rel)


def read(rel):
    return open(path(rel), encoding="utf-8").read()


def edit_list(text, header, drop=(), add=None):
    """Rewrite one of a .mat's `m_Floats:` / `m_Colors:` blocks, keeping it sorted."""
    m = re.search(r"^(    %s:\n)((?:    - .*\n(?:        .*\n)*)*)" % re.escape(header),
                  text, re.M)
    assert m, "no %s block" % header
    rows = re.findall(r"    - (_[A-Za-z0-9_]+): (.*)\n", m.group(2))
    kept = {k: v for k, v in rows if k not in drop}
    if add:
        kept.update(add)
    body = "".join("    - %s: %s\n" % (k, kept[k]) for k in sorted(kept))
    return text[:m.start()] + m.group(1) + body + text[m.end():]


def make_fauna_material():
    src = read(SPINDLE_MAT)
    out = src.replace("m_Name: SpindleMaterial", "m_Name: FaunaSpindleMaterial")
    assert "m_Name: FaunaSpindleMaterial" in out, "SpindleMaterial's name line moved"
    before = out
    out = out.replace(SPINDLE_GRAPH_GUID, FAUNA_GRAPH_GUID)
    assert out != before, "SpindleMaterial does not reference SpindleGraph"
    out = edit_list(out, "m_Floats", add=NEW_FLOATS)
    out = edit_list(out, "m_Colors", drop=DROP_COLORS, add=NEW_COLORS)
    return out


def make_quadfish_material():
    src = read(QUADFISH_MAT)
    out = src.replace(SPINDLE_GRAPH_GUID, FAUNA_GRAPH_GUID)
    assert out != src or FAUNA_GRAPH_GUID in src, "QuadFishSpindleMaterial is on neither graph"
    out = edit_list(out, "m_Floats", add=NEW_FLOATS)
    out = edit_list(out, "m_Colors", drop=DROP_COLORS, add=NEW_COLORS)
    return out


def make_referrer(rel, expected):
    src = read(rel)
    n = src.count(SPINDLE_MAT_GUID) + src.count(FAUNA_MAT_GUID)
    assert n == expected, \
        "%s references the spindle material %d times, expected %d — re-read it" % (rel, n, expected)
    return src.replace(SPINDLE_MAT_GUID, FAUNA_MAT_GUID)


def meta_for(guid):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n" % guid)


def validate(planned):
    """Everything asserted BEFORE a byte is written."""
    problems = []

    fauna = planned[FAUNA_MAT]
    quad = planned[QUADFISH_MAT]
    for name, text in ((FAUNA_MAT, fauna), (QUADFISH_MAT, quad)):
        if SPINDLE_GRAPH_GUID in text:
            problems.append("%s still references SpindleGraph" % name)
        if "guid: %s" % FAUNA_GRAPH_GUID not in text:
            problems.append("%s does not reference FaunaSpindleGraph" % name)
        if SHADERGRAPH_SUBASSET not in text:
            problems.append("%s lost the shadergraph sub-asset fileID" % name)
        for key in DROP_COLORS:
            if re.search(r"    - %s:" % key, text):
                problems.append("%s still authors %s — it is a global now" % (name, key))
        for key in list(NEW_FLOATS) + list(NEW_COLORS):
            if not re.search(r"    - %s:" % key, text):
                problems.append("%s does not author %s" % (name, key))
        for header in ("m_Floats", "m_Colors"):
            m = re.search(r"^    %s:\n((?:    - .*\n(?:        .*\n)*)*)" % header, text, re.M)
            keys = re.findall(r"    - (_[A-Za-z0-9_]+):", m.group(1))
            if keys != sorted(keys):
                problems.append("%s's %s block is not sorted" % (name, header))

    # the sway numbers are NOT this script's to change
    for name, text, amp, freq in ((FAUNA_MAT, fauna, "0.08", "1.4"),
                                  (QUADFISH_MAT, quad, "0.13", "5.2")):
        for key, want in (("_SwayAmplitude", amp), ("_SwayFrequency", freq)):
            m = re.search(r"    - %s: ([-0-9.]+)" % key, text)
            if not m or m.group(1) != want:
                problems.append("%s %s is %s, expected the shipped %s"
                                % (name, key, m.group(1) if m else "absent", want))

    for rel in FAUNA_REFERRERS:
        if SPINDLE_MAT_GUID in planned[rel]:
            problems.append("%s still references SpindleMaterial" % rel)
        if FAUNA_MAT_GUID not in planned[rel]:
            problems.append("%s does not reference FaunaSpindleMaterial" % rel)

    # the flora side must be untouched, and the sweep must still agree with the lists
    live = set()
    for root, _dirs, files in os.walk(os.path.join(REPO, "Assets")):
        for f in files:
            if not f.endswith((".prefab", ".unity", ".asset", ".mat")):
                continue
            p = os.path.join(root, f)
            rel = os.path.relpath(p, REPO)
            try:
                text = open(p, encoding="utf-8", errors="ignore").read()
            except Exception:
                continue
            if SPINDLE_MAT_GUID in text:
                live.add(rel)
    unexpected = live - FLORA_AND_OTHER - set(FAUNA_REFERRERS)
    if unexpected:
        problems.append("SpindleMaterial has referrers this script has never classified: %s"
                        % sorted(unexpected))
    missing = FLORA_AND_OTHER - live
    if missing:
        problems.append("a flora/other referrer vanished: %s" % sorted(missing))

    return problems


def main():
    check_only = "--check" in sys.argv

    planned = {FAUNA_MAT: make_fauna_material(), QUADFISH_MAT: make_quadfish_material()}
    for rel, n in FAUNA_REFERRERS.items():
        planned[rel] = make_referrer(rel, n)

    problems = validate(planned)
    if problems:
        for p in problems:
            print("FAIL: %s" % p, file=sys.stderr)
        sys.exit(1)

    drift = [rel for rel, text in planned.items()
             if not os.path.exists(path(rel)) or read(rel) != text]
    if not drift:
        print("all %d files up to date" % len(planned))
        print("OK")
        return
    if check_only:
        for rel in drift:
            print("DRIFTED: %s" % rel, file=sys.stderr)
        sys.exit(1)

    for rel in drift:
        with open(path(rel), "w", encoding="utf-8") as fh:
            fh.write(planned[rel])
        print("  wrote %s" % rel)
    meta = path(FAUNA_MAT + ".meta")
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8") as fh:
            fh.write(meta_for(FAUNA_MAT_GUID))
        print("  wrote %s.meta" % FAUNA_MAT)
    print("OK")


if __name__ == "__main__":
    main()
